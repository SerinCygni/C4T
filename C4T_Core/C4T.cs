using TwitchLib.Client.Models;
using TwitchLib.Client;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using System.Net;
using System.Net.Http.Json;
using C4T_Core.Helpers;
using TwitchLib.Client.Events;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using System.Collections;
using System.Text.Json;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.ComponentModel;
using TwitchLib.EventSub.Websockets;
using System.Net.WebSockets;
using Websocket.Client;
using Microsoft.VisualBasic;
using SocketIOClient;
using System.Runtime.CompilerServices;

namespace C4T_Core
{
    public class C4T
    {
        private TwitchClient? client;
        private TwitchPubSub? psClient;
        private SocketIO? slClient;
        private C4TOptions options;
        private AudioController audioController;
        private Random rng = new Random();
        private Hashtable dailyFirstChatters = new Hashtable();

        public bool Initialized = false;

        public async Task SaveOptions(C4TOptions opt)
        {
            var filePath = Path.Combine(opt.AppDataPath, "options.json");
            var optionsSerialized = JsonSerializer.Serialize(opt);
            await File.WriteAllTextAsync(filePath, optionsSerialized);
        }

        public static C4TOptions LoadOptions(string appDataPath)
        {
            var filePath = Path.Combine(appDataPath, "options.json");

            C4TOptions? options = null;

            if (File.Exists(filePath))
            {
                var optionsSerialized = File.ReadAllText(filePath);
                options = JsonSerializer.Deserialize<C4TOptions>(optionsSerialized);
            }

            return options ?? new C4TOptions()
            {
                ClientId = "",
                ClientSecret = "",
                SpeechKey = "",
                ChatChannel = "",
                AppDataPath = appDataPath,
                UseStreamlabs = true,
                FollowReact = true,
                RaidReact = true,
                DailyMessageReact = true,
                UseTextToSpeech = true,
                SpeechSynthesisVoiceName = "en-US-SaraNeural"
            };

        }


        public async Task Initialize(C4TOptions opt)
        {
            options = opt;

            /*    Configure Twitch chat   */
            var accessToken = await GetTwitchAccessToken();
            ConnectionCredentials credentials = new ConnectionCredentials("ai_c4t", accessToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, options.ChatChannel);
            client.OnConnected += OnConnected;
            client.OnLog += OnLog;

            /*   Configure Twitch PubSub   */
            psClient = new TwitchPubSub();
            psClient.OnPubSubServiceConnected += onPubSubServiceConnected;


            /*   Configure bot features    */
            client.OnMessageReceived += ProcessMessage;
            if (options.FollowReact)
            {
                psClient.ListenToFollows("36397628");
                psClient.OnFollow += HandleNewFollow;
            }
            if (options.RaidReact)
            {
                client.OnRaidNotification += HandleRaid;
            }

            /*    Configure Audio Options   */
            if (options.UseTextToSpeech)
            {
                audioController = new AudioController(options);
            }

            /*   Connect    */
            var clientConnect = client.Connect();
            var connected = client.IsConnected;
            psClient.Connect();

            Initialized = true;
        }

        private async Task<string?> GetTwitchAccessToken()
        {
            string code = "";
            string filePath = Path.Combine(options?.AppDataPath ?? "", "refreshtoken.txt");
            if (!File.Exists(filePath))
            {
                /* This part sucks, will have to rewrite to include user consent in a web view */

                HttpClient hc = new();
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("client_id", options.ClientId),
                    new KeyValuePair<string,string>("client_secret", options.ClientSecret),
                    new KeyValuePair<string,string>("redirect_uri", "http://localhost:3000"),
                    new KeyValuePair<string,string>("grant_type", "authorization_code"),
                    new KeyValuePair<string,string>("code", "ml9c75p2l7al9owf6a7h61eroz3w9f"),
                });
                var response = await hc.PostAsync("https://id.twitch.tv/oauth2/token", content);
                var tar = await response.Content.ReadFromJsonAsync<TwitchAuthResponse>(new JsonSerializerOptions(JsonSerializerDefaults.General));
                await File.WriteAllLinesAsync(filePath, new string[] { tar?.refresh_token ?? "" });
                return tar?.access_token;
            }
            else
            {
                code = (await File.ReadAllLinesAsync(filePath)).First();
                HttpClient hc = new();
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("client_id", options.ClientId),
                    new KeyValuePair<string,string>("client_secret", options.ClientSecret),
                    new KeyValuePair<string,string>("refresh_token", code),
                    new KeyValuePair<string,string>("grant_type", "refresh_token")
                });
                var response = await hc.PostAsync("https://id.twitch.tv/oauth2/token", content);
                var tar = await response.Content.ReadFromJsonAsync<TwitchAuthResponse>(new JsonSerializerOptions(JsonSerializerDefaults.General));
                await File.WriteAllLinesAsync(filePath, new string[] { tar?.refresh_token ?? "" });
                return tar?.access_token;
            }
        }

        private void OnLog(object? sender, TwitchLib.Client.Events.OnLogArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private void onPubSubServiceConnected(object? sender, EventArgs e)
        {
            psClient?.SendTopics();
        }

        private void OnConnected(object? sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Joined channel {e.AutoJoinChannel}");
        }

        private void ProcessMessage(object? sender, OnMessageReceivedArgs e)
        {
            if (options.DailyMessageReact && CheckFirstDailyMessage(e.ChatMessage.UserId))
            {
                HandleFirstDailyMessage(e.ChatMessage.Id, e.ChatMessage.DisplayName);
            }

            if (e.ChatMessage.Message[0] == '!')
            {
                var commandSplit = e.ChatMessage.Message.Split(' ', 2);
                var command = commandSplit[0];
                var parms = commandSplit.Length > 1 ? commandSplit[1] : null;
                switch (command)
                {
                    case "!coinflip":
                        DoCoinFlip(parms);
                        break;
                    case "!catgirl":
                        DoCatGirl(e.ChatMessage.DisplayName);
                        break;
                    default:
                        break;
                }
            }
        }

        private void HandleNewFollow(object? sender, OnFollowArgs e)
        {
            string ssml;
            var randomFollowChoice = rng.Next() % 4;
            switch (randomFollowChoice)
            {
                case 0:
                    ssml = ssmlEntries.follow1.Replace("{follower}", e.DisplayName);
                    break;
                case 1:
                    ssml = ssmlEntries.follow2.Replace("{follower}", e.DisplayName);
                    break;
                case 2:
                    ssml = ssmlEntries.follow3.Replace("{follower}", e.DisplayName);
                    break;
                case 3:
                    ssml = ssmlEntries.follow4.Replace("{follower}", e.DisplayName);
                    break;
                default:
                    ssml = ssmlEntries.follow1.Replace("{follower}", e.DisplayName);
                    break;
            }

            audioController.AddToQueue(ssml);
            return;
        }

        private void HandleRaid(object? sender, OnRaidNotificationArgs e)
        {
            var incomingRaidChannel = e.RaidNotification.MsgParamDisplayName;
            var ssml = ssmlEntries.raid1.Replace("{raider}", incomingRaidChannel);
            audioController.AddToQueue(ssml);
            client?.SendMessage(options.ChatChannel, $"Hello raiders! Thanks for the raid, @{incomingRaidChannel}! Check them out at https://twitch.tv/{incomingRaidChannel}");
        }

        private bool CheckFirstDailyMessage(string userId)
        {
            if (dailyFirstChatters.ContainsKey(userId))
            {
                return false;
            }
            else
            {
                dailyFirstChatters.Add(userId, true);
                return true;
            }
        }

        private void HandleFirstDailyMessage(string replyToId, string username)
        {
            client?.SendReply(options.ChatChannel, replyToId, $"Good to see you, {username}. Enjoy your stay!");
        }

        private void DoCoinFlip(string? parms)
        {
            var choice1 = "heads";
            var choice2 = "tails";
            if (parms?.Contains("|") ?? false)
            {
                var choices = parms.Split("|", 2);
                choice1 = choices[0];
                choice2 = choices[1];
            }

            var rand = rng.Next() % 2;
            var headsOrTails = rand == 0 ? choice1 : choice2;
            client?.SendMessage(options.ChatChannel, $"I flipped a coin and got {headsOrTails}.");
            var ssml = ssmlEntries.coinflip.Replace("{$result}", headsOrTails);
            audioController.AddToQueue(ssml);

        }

        private void DoCatGirl(string displayName)
        {
            audioController.AddToQueue(ssmlEntries.catgirl.Replace("{$chatter}", displayName));
        }
    }
}