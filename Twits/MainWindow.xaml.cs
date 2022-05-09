namespace Twits
{
    using AdonisUI.Controls;
    using System;
    using System.Management.Automation;
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Toolkit.Uwp.Notifications;
    using Newtonsoft.Json;

    public partial class MainWindow : AdonisWindow
    {
        internal LoginWindow loginWindow;

        internal PowerShell pipeline;
        internal PowerShell ThreadPipelineOne;
        internal PowerShell ThreadPipelineTwo;

        internal string username = "";
        internal string userId = "";
        internal List<FollowedTwitchUser> followedUsers;
        internal Timer checkNewFollowedTimer;
        internal Timer checkNewLiveStreamersTimer;

        int FOLLOW_TIMER_MS = 10000;
        int LIVESTREAMER_TIMER_MS = 10000;

        public MainWindow()
        {
            pipeline = PowerShell.Create();
            ThreadPipelineOne = PowerShell.Create();
            ThreadPipelineTwo = PowerShell.Create();
            loginWindow = new LoginWindow();
            InitializeComponent();
        }

        private void GetLoginCredentials()
        {
            loginWindow.ShowDialog();
            username = loginWindow.UsernameInput.Text;
        }

        string InvokationToString(System.Collections.ObjectModel.Collection<PSObject> returnObject)
        {
            string output = "";
            foreach(var item in returnObject)
            {
                output += item.ToString();
            }
            return output;
        }

        private string FetchUserId(string username)
        {
            pipeline.AddScript("twitch api get users -q login=" + username);
            System.Collections.ObjectModel.Collection<PSObject> result = pipeline.Invoke();
            if(result.Count == 3)
            {
                AdonisUI.Controls.MessageBox.Show("User: " + username + " does not exist. Notifications are disabled.");
            }
            return JObject.Parse(InvokationToString(result))["data"].First["id"].ToString();
        }

        private List<FollowedTwitchUser> FetchFollowedUsers(string userId)
        {
            ThreadPipelineOne.AddScript("twitch api get users/follows -q from_id=" + userId);
            JObject jsonResults = JObject.Parse(InvokationToString(ThreadPipelineOne.Invoke()));
            List<FollowedTwitchUser> followedUsers = new List<FollowedTwitchUser>();
            foreach( var user in jsonResults["data"] )
            {
                ThreadPipelineOne.AddScript("twitch api get users -q login=" + (string)user["to_name"]);
                string result = InvokationToString(ThreadPipelineOne.Invoke());
                if (result != "{  \"data\": []}")
                {
                    Uri profilePictureUri = new Uri((string)JObject.Parse(result)["data"].First["profile_image_url"], UriKind.Absolute);
                    followedUsers.Add(new FollowedTwitchUser((string)user["to_name"], (string)user["to_id"], profilePictureUri));
                }  
            }
            return followedUsers;
        }

        private void TwitchApiStart()
        {
            pipeline.AddScript("twitch configure --client-id 4v8e9ixyim5umwx12a3nr25o3r0wcx --client-secret 7eiu3ndmtgvqcii8pp7jeeltma5c2m");
            var result = pipeline.Invoke();
            if (result[0].ToString() == "Updated configuration.")
            {
                //TODO: Add something to test if secret/client ID were valid.
                Console.WriteLine("Twitch API logged on");
            }
        }

        private void CheckNewFollowedUserCallback(object state)
        {
            followedUsers = FetchFollowedUsers(userId);
            checkNewFollowedTimer.Change(FOLLOW_TIMER_MS, 0);
        }

        private void CheckLiveStreamersCallback(object state)
        {
            foreach (FollowedTwitchUser streamer in followedUsers)
            {
                ThreadPipelineTwo.AddScript("twitch api get streams -q user_login=" + streamer.name);
                JObject result = JObject.Parse(InvokationToString(ThreadPipelineTwo.Invoke()));
                if(result.Count > 2)
                {
                    var streamerStatus = JsonConvert.DeserializeObject<dynamic>(result.First.ToString());
                    if (streamerStatus.live)
                    {
                        new ToastContentBuilder().AddInlineImage(streamer.picture).AddText(streamer.name + " is live!", AdaptiveTextStyle.Title).AddText(streamerStatus.title).Show();
                    }
                }
            }
            checkNewLiveStreamersTimer.Change(LIVESTREAMER_TIMER_MS, 0);
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
                if (!InstallCLIDepedencies())
                {
                    AdonisUI.Controls.MessageBox.Show("API installation error, notifications will not work.");
                }
                TwitchApiStart();
                userId = FetchUserId(username);
                followedUsers = FetchFollowedUsers(userId);
                checkNewFollowedTimer = new Timer(CheckNewFollowedUserCallback, null, FOLLOW_TIMER_MS, 0);
                checkNewLiveStreamersTimer = new Timer(CheckLiveStreamersCallback, null, LIVESTREAMER_TIMER_MS, 1000);
            }
        }

        private void CheckNewFollowed(object state)
        {
            
        }

        private void CheckLiveStreams()
        {

        }

        private void AdonisWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private bool InstallScoop()
        {
            pipeline.AddScript("scoop");
            try
            {
                pipeline.Invoke();
                // Already Installed
                Console.WriteLine("Scoop was already installed on this device");
                return true;
            }
            catch (System.Management.Automation.CommandNotFoundException)
            {
                // Install Scoop
                pipeline.Commands.Clear();
                pipeline.AddCommand("Set-ExecutionPolicy RemoteSigned -Scope CurrentUser");
                pipeline.AddCommand("Invoke-WebRequest get.scoop.sh | Invoke-Expression");
                try
                {
                    pipeline.Invoke();
                    Console.WriteLine("Installation of scoop succeeded");
                    return true;
                }
                catch
                {
                    // Installation Failed, why?
                    Console.WriteLine("Installation of scoop failed");
                    return false;
                }
            }
        }

        private bool InstallCLIDepedencies()
        {
            return InstallScoop() && InstallTwitchApi();
        }

        private bool InstallTwitchApi()
        {
            pipeline.Commands.Clear();
            try
            {
                pipeline.AddScript("twitch -h");
                pipeline.Invoke();
                Console.WriteLine("Twitch CLI was already installed on this device");
                return true;
            }
            catch (System.Management.Automation.CommandNotFoundException)
            {
                try
                {
                    pipeline.AddCommand("scoop bucket add twitch https://github.com/twitchdev/scoop-bucket.git");
                    pipeline.AddCommand("scoop install twitch-cli");
                    pipeline.Invoke();
                    Console.WriteLine("Installation of twitch API succeeded");
                    return true;
                }
                catch
                {
                    Console.WriteLine("Installation of twitch API failed");
                    return false;
                }
            }
        }
    }
}
