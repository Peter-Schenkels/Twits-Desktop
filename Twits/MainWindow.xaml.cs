namespace Twits
{
    using AdonisUI.Controls;
    using Microsoft.Toolkit.Uwp.Notifications;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using TwitchLib.Api;


    public partial class MainWindow : AdonisWindow
    {
        private TwitchLib.Api.TwitchAPI API;
        private string username = "";
        private string userId = "";
        private List<FollowedTwitchUser> followedUsers;
        private int UPDATE_TIMER_MS = 10000;

        public MainWindow()
        {
            API = new TwitchLib.Api.TwitchAPI();
            API.Settings.ClientId = TwitchAPI.Client.key;
            API.Settings.Secret = TwitchAPI.Secret.key;
            InitializeComponent();
        }

        private void OnInitialized(object sender, EventArgs args)
        { 
            _ = UpdateTwitchUserState();  
        }
        private async Task<bool> RetreiveUsername()
        {
            string returnName = "";
            try
            {
                await Dispatcher.Invoke(async () =>
                {
                    if (wvc.IsInitialized)
                    {
                        string res = await wvc.ExecuteScriptAsync("cookies.login");
                        Debug.WriteLine("cookies.login = " + res);
                        if (res != "null")
                        {
                            returnName = JToken.Parse(res).Value<string>();
                            this.username = returnName;
                        }                          
                    }
                });
                return returnName != "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error inside your async code!");
                return false;
            }
            
        }

        private async Task UpdateTwitchUserState()
        {
            while(!await Task.Run(RetreiveUsername))
            {
                await Task.Delay(100);
            }
            var users = await API.Helix.Users.GetUsersAsync(logins: new List<string> { this.username });
            this.userId = users.Users[0].Id;
            DateTime lastCheck = new DateTime();
            while(true)
            { 
                var usersFollows = await API.Helix.Users.GetUsersFollowsAsync(fromId: this.userId);
                this.followedUsers = usersFollows.Follows
                    .Select(x => new Twits.FollowedTwitchUser(x.ToUserName, x.ToUserId, null))
                    .ToList();
                var streams = await API.Helix.Streams.GetStreamsAsync(userIds: this.followedUsers.Select(x => x.id).ToList());
                List<FollowedTwitchUser> checkedUsers = this.followedUsers.ToList();

                foreach (var streamer in streams.Streams)
                {
                    if (lastCheck < streamer.StartedAt)
                    {
                        string thumbnailPath = Path.Combine(Path.GetTempPath(), "twits - thumbnail.png");
                        using (WebClient webClient = new WebClient())
                        {
                            webClient.DownloadFile(streamer.ThumbnailUrl.Replace("{width}", "300").Replace("{height}", "200"), thumbnailPath);
                        }

                        Path.Combine(Path.GetTempPath(), "twits-thumbnail.png");
                        new ToastContentBuilder()
                            .AddText(streamer.UserName + " is live!", AdaptiveTextStyle.Title)
                            .AddText(streamer.Title)
                            .AddInlineImage(new Uri(thumbnailPath)).Show();
                    }
                }
                lastCheck = DateTime.UtcNow;
                await Task.Delay(UPDATE_TIMER_MS);
            }
        }
    }
}
