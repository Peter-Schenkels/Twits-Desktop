namespace Twits
{
    using AdonisUI.Controls;
    using Microsoft.Toolkit.Uwp.Notifications;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;
    using System.Threading.Tasks;
    using TwitchLib.Api;

    public partial class MainWindow : AdonisWindow
    {

        internal LoginWindow loginWindow;
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
            loginWindow = new LoginWindow();
            InitializeComponent();
        }

        private void GetLoginCredentials()
        {
            loginWindow.ShowDialog();
            username = loginWindow.UsernameInput.Text;
        }

        private void OnInitialized(object sender, EventArgs args)
        {
            GetLoginCredentials();
            if (username == "")
            {
                AdonisUI.Controls.MessageBox.Show("Can't show notifications if no username is given");
            }
            else
            {
                _ = UpdateTwitchUserState();
            }
        }

        private async Task UpdateTwitchUserState()
        {
            if (this.username == "") return;
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
                        new ToastContentBuilder()
                            .AddInlineImage(new Uri(streamer.ThumbnailUrl, UriKind.Absolute))
                            .AddText(streamer.UserName + " is live!", AdaptiveTextStyle.Title)
                            .AddText(streamer.Title).Show();
                    }
                }
                lastCheck = DateTime.UtcNow;
                await Task.Delay(UPDATE_TIMER_MS);
            }
        }
    }
}
