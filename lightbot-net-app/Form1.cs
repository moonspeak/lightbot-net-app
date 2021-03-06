﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Q42.HueApi;
using Q42.HueApi.Interfaces;
using System.Threading;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.Original;
using ServiceStack.Redis;
using Newtonsoft.Json;
using AutoUpdaterDotNET;
using System.Text.RegularExpressions;

namespace lightbot_net_app
{
    public partial class Form1 : Form
    {
        public IEnumerable<Q42.HueApi.Models.Bridge.LocatedBridge> bridgeIPs;
        public IBridgeLocator locator = new HttpBridgeLocator();
        public ILocalHueClient client;
        public IReadOnlyCollection<Q42.HueApi.Models.Groups.Group> groups;
        public Thread pubsubThread;

        private IRedisPubSubServer redisPubSub;

        public Form1()
        {
            InitializeComponent();
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.ReportErrors = true;
            AutoUpdater.Start(@"http://updates.huelightbot.com/updates/AutoUpdater.xml");
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.Visible == false)
            {
                this.ShowDialog();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            /// Checkboxes
            checkBox1.Checked = Properties.Settings.Default.cheering;
            checkBox2.Checked = Properties.Settings.Default.largeCheer;
            checkBox3.Checked = Properties.Settings.Default.onOff;
            checkBox4.Checked = Properties.Settings.Default.subs;
            checkBox5.Checked = Properties.Settings.Default.setlightsMods;
            checkBox6.Checked = Properties.Settings.Default.setlightsSubs;
            checkBox7.Checked = Properties.Settings.Default.colorloopMods;
            checkBox8.Checked = Properties.Settings.Default.colorloopSubs;
            subGiftCheckBox.Checked = Properties.Settings.Default.giftSubs;
            tier1subColorCheckBox.Checked = Properties.Settings.Default.Tier1ChangeColor;
            tier2SubComboBox.Checked = Properties.Settings.Default.Tier2ChangeColor;
            tier3ColorBox.Checked = Properties.Settings.Default.Tier3ChangeColor;
            primeSubColorBox.Checked = Properties.Settings.Default.PrimeSubChangeColor;
            raidCheckBox.Checked = Properties.Settings.Default.raids;

            /// Textboxes
            textBox1.Text = Properties.Settings.Default.channel.ToString();
            textBox2.Text = Properties.Settings.Default.cheerFloor.ToString();
            textBox3.Text = Properties.Settings.Default.largeCheerFloor.ToString();
            textBox4.Text = Properties.Settings.Default.offFloor.ToString();
            textBox5.Text = Properties.Settings.Default.onFloor.ToString();

            /// Comboboxes
            comboBox1.Text = Properties.Settings.Default.largeCheerAction;
            comboBox2.Text = Properties.Settings.Default.primeSubAction;
            comboBox3.Text = Properties.Settings.Default.Tier1SubAction;
            comboBox4.Text = Properties.Settings.Default.Tier2SubAction;
            comboBox5.Text = Properties.Settings.Default.Tier3SubAction;
            giftSubT1comboBox.Text = Properties.Settings.Default.giftSubT1;
            giftSubT2comboBox.Text = Properties.Settings.Default.giftSubT2;
            giftSubT3comboBox.Text = Properties.Settings.Default.giftSubT3;
            raidActionComboBox.Text = Properties.Settings.Default.raidAction;


            colorLoopDurationUpDown.Value = Properties.Settings.Default.colorLoopDuration;
            exitToolStripMenuItem.Click += new EventHandler(exitToolStripMenuItem_Click);

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
            Application.Exit();
            return;
        }

        private void label10_Click(object sender, EventArgs e)
        {

        }

        private async void button4_Click(object sender, EventArgs e)
        {
            bridgeIPs = await locator.LocateBridgesAsync(TimeSpan.FromSeconds(10));
            try
            {
                client = new LocalHueClient(bridgeIPs.First().IpAddress);
                if (!string.IsNullOrEmpty(Properties.Settings.Default.appkey))
                {
                    client.Initialize(Properties.Settings.Default.appkey);
                    logEvent("Connected with previous Hue Auth");
                }
                else
                {
                    string messageBoxText = "Press the button on your Hue bridge and then click Ok.";
                    string caption = "Action Required";
                    MessageBoxButtons button = MessageBoxButtons.OK;
                    DialogResult result = MessageBox.Show(messageBoxText, caption, button);

                    switch (result)
                    {
                        case DialogResult.OK:
                            var appKey = await client.RegisterAsync("HueLightBot", Environment.MachineName);
                            Properties.Settings.Default.appkey = appKey;
                            Properties.Settings.Default.Save();
                            logEvent("Connected with new Hue Auth");
                            break;
                    }
                }

                groups = await client.GetGroupsAsync();
                List<string> groupNames = groups.Select(x => x.Name).ToList();

                comboBox6.DataSource = groupNames;
            }
            catch (Exception exception)
            {
                string messageBoxText;
                string caption;
                if (exception.Message == "Link button not pressed")
                {
                    messageBoxText = "Link Button not pressed or pairing didn't occur in time. Please try again.";
                    caption = "Error";
                }
                else
                {
                    messageBoxText = string.Format("An uncaught exception occured. Please let the devs know the following: {0}", Environment.NewLine + exception.Message);
                    caption = "Error";
                }

                MessageBoxButtons button = MessageBoxButtons.OK;
                DialogResult result = MessageBox.Show(messageBoxText, caption, button);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            /// Checkboxes
            Properties.Settings.Default.cheering = checkBox1.Checked;
            Properties.Settings.Default.largeCheer = checkBox2.Checked;
            Properties.Settings.Default.onOff = checkBox3.Checked;
            Properties.Settings.Default.giftSubs = subGiftCheckBox.Checked;
            Properties.Settings.Default.subs = checkBox4.Checked;
            Properties.Settings.Default.setlightsMods = checkBox5.Checked;
            Properties.Settings.Default.setlightsSubs = checkBox6.Checked;
            Properties.Settings.Default.colorloopMods = checkBox7.Checked;
            Properties.Settings.Default.colorloopSubs = checkBox8.Checked;
            Properties.Settings.Default.Tier1ChangeColor = tier1subColorCheckBox.Checked;
            Properties.Settings.Default.Tier2ChangeColor = tier2SubComboBox.Checked;
            Properties.Settings.Default.Tier3ChangeColor = tier3ColorBox.Checked;
            Properties.Settings.Default.PrimeSubChangeColor = primeSubColorBox.Checked;
            Properties.Settings.Default.raids = raidCheckBox.Checked;

            /// Textboxes
            Properties.Settings.Default.channel = textBox1.Text;
            Properties.Settings.Default.cheerFloor = Int32.Parse(textBox2.Text);
            Properties.Settings.Default.largeCheerFloor = Int32.Parse(textBox3.Text);
            Properties.Settings.Default.offFloor = Int32.Parse(textBox4.Text);
            Properties.Settings.Default.onFloor = Int32.Parse(textBox5.Text);

            /// Comboboxes
            Properties.Settings.Default.largeCheerAction = comboBox1.Text;
            Properties.Settings.Default.primeSubAction = comboBox2.Text;
            Properties.Settings.Default.Tier1SubAction = comboBox3.Text;
            Properties.Settings.Default.Tier2SubAction = comboBox4.Text;
            Properties.Settings.Default.Tier3SubAction = comboBox5.Text;
            Properties.Settings.Default.colorLoopDuration = colorLoopDurationUpDown.Value;
            Properties.Settings.Default.giftSubT1 = giftSubT1comboBox.Text;
            Properties.Settings.Default.giftSubT2 = giftSubT2comboBox.Text;
            Properties.Settings.Default.giftSubT3 = giftSubT3comboBox.Text;
            Properties.Settings.Default.raidAction = raidActionComboBox.Text;

            Properties.Settings.Default.Save();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            /// stop
            if (redisPubSub != null)
            {
                logEvent("Stopping LightBot!",true);
                redisPubSub.Stop();
                redisPubSub = null;
                pubsubThread = null;
                toolStripStatusLabel2.Text = "Disconnected";
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            /// start
            if (pubsubThread == null)
            {
                logEvent("Starting LightBot!",true);
                pubsubThread = (new Thread(() => pubsubRunner(client)));
                pubsubThread.Start();
                toolStripStatusLabel2.Text = "Connected";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                return;
            }
            else
            {
                this.Visible = false;
                e.Cancel = true;
            }
        }

        private void pubsubRunner(ILocalHueClient client)
        {
            Console.WriteLine("Started pubsub");

            var clientsManager = new PooledRedisClientManager("client:3rogK4bOMIAFDSJ8P0FqjNYxwDIk6UatbbMlUVV7uySHnPhxhrh0IFIwnf3ZEjo@pubsub.huelightbot.com");
            
            redisPubSub = new RedisPubSubServer(clientsManager, textBox1.Text)
            {
                OnMessage = (channel, msg) => HandleOnMessage(channel, msg),
                OnError = (ex) => HandleOnError(ex)
            }.Start();

            return;
        }

        private void HandleOnError(Exception ex)
        {
            logEvent(string.Format("Received '{0}' error from pubsub", ex.Message));

        }

        private void HandleOnMessage(string channel, string msg)
        {

            logEvent(string.Format("Received '{0}' from pubsub", msg, channel));

            if (msg.Contains("\"type\":\"cheer\""))
            {
                CheerVO cheer = JsonConvert.DeserializeObject<CheerVO>(msg);
                HandleAction(cheer);

            }
            else if (msg.Contains("\"type\":\"command\""))
            {
                CommandVO commandVO = JsonConvert.DeserializeObject<CommandVO>(msg);
                HandleAction(commandVO);

            }
            else if (msg.Contains("\"type\":\"sub\""))
            {
                SubVO subVO = JsonConvert.DeserializeObject<SubVO>(msg);
                HandleAction(subVO);
            }
            else if (msg.Contains("\"type\":\"raid\""))
            {
                RaidVO raidVO = JsonConvert.DeserializeObject<RaidVO>(msg);
                HandleAction(raidVO);
            }
            else
            {
                // wtf
            }
        }

        private void HandleAction(RaidVO raidVO)
        {
            if (Properties.Settings.Default.raids)
            {
                bool doColorLoop = false;
                bool doBlink = false;

                doColorLoop = Properties.Settings.Default.raidAction.ToLower() == "loop";
                doBlink = Properties.Settings.Default.raidAction.ToLower() == "blink";

                if (doColorLoop) DoColorLoop();
                if (doBlink) DoBlink();
            }

            return;
        }

        private void HandleAction(CommandVO commandVO)
        {
            HandleSetLightsCommand(commandVO);
            HandleColorLoop(commandVO);
        }

        private void HandleSetLightsCommand(CommandVO commandVO)
        {
            if (commandVO.message.Contains("!setlights"))
            {
                if (Properties.Settings.Default.channel == commandVO.nick)
                {
                    foreach (Match match in Regex.Matches(commandVO.message, @"#([0-9a-fA-F]{6})"))
                    {
                        if (!string.IsNullOrEmpty(match.Value))
                            SetHexColor(match.Value);
                    }
                }
                else if (commandVO.mod)
                {
                    if (Properties.Settings.Default.setlightsMods)
                    {
                        foreach (Match match in Regex.Matches(commandVO.message, @"#([0-9a-fA-F]{6})"))
                        {
                            if (!string.IsNullOrEmpty(match.Value))
                                SetHexColor(match.Value);
                        }
                    }
                }
                else if (commandVO.sub)
                {
                    if (Properties.Settings.Default.setlightsSubs)
                    {
                        foreach (Match match in Regex.Matches(commandVO.message, @"#([0-9a-fA-F]{6})"))
                        {
                            if (!string.IsNullOrEmpty(match.Value))
                                SetHexColor(match.Value);
                        }
                    }
                }

            }
        }

        private void HandleColorLoop(CommandVO commandVO)
        {
            if (commandVO.message.Contains("!colorloop"))
            {
                if (Properties.Settings.Default.channel == commandVO.nick)
                {
                    DoColorLoop();
                }
                else if (commandVO.mod)
                {
                    if (Properties.Settings.Default.colorloopMods)
                    {
                        DoColorLoop();
                    }
                }
                else if (commandVO.sub)
                {
                    if (Properties.Settings.Default.colorloopSubs)
                    {
                        DoColorLoop();
                    }
                }

            }
        }

        private void HandleAction(CheerVO cheer)
        {
            logEvent(String.Format("{0} cheered {1} bits!", cheer.nick, cheer.amount));
            if (cheer.amount >= Properties.Settings.Default.largeCheerFloor && Properties.Settings.Default.largeCheer)
            {
                if (Properties.Settings.Default.largeCheerAction.ToLower() == "blink")
                {
                    DoBlink();
                }
                else if (Properties.Settings.Default.largeCheerAction.ToLower() == "loop")
                {
                    DoColorLoop();
                }

            }
            if (cheer.amount >= Properties.Settings.Default.cheerFloor)
            {
                int count = 1;
                foreach (Match match in Regex.Matches(cheer.message, @"#([0-9a-fA-F]{6})"))
                {

                    if(count* Properties.Settings.Default.cheerFloor <= cheer.amount)
                        if (!string.IsNullOrEmpty(match.Value))
                            SetHexColor(match.Value);
                            Thread.Sleep(3000);
                    count++;
                }

            }



            if (cheer.amount >= Properties.Settings.Default.offFloor && cheer.message.Contains("!off") && Properties.Settings.Default.onOff)
            {
                SetLightsOff();
            }
            else if (cheer.amount >= Properties.Settings.Default.onFloor && cheer.message.Contains("!on") && Properties.Settings.Default.onOff)
            {
                SetLightsOn();
            }
        }

        private void HandleAction(SubVO subVO)
        {

            logEvent(String.Format("{0} just subscribed at tier {1}",subVO.nick,subVO.tier));

            SubTiers subTier = SubTiers.Unassigned;
            switch (subVO.tier.ToLower())
            {
                case "prime":
                    subTier = SubTiers.Prime;
                    break;
                case "1000":
                    subTier = SubTiers.Tier1;
                    break;
                case "2000":
                    subTier = SubTiers.Tier2;
                    break;
                case "3000":
                    subTier = SubTiers.Tier3;
                    break;
            }

            if (subVO.gift)
            {
                if (Properties.Settings.Default.giftSubs)
                {
                    HandleGiftSubAction(subTier);
                }
            }
            else
            {
                bool doColors = HandleSubAction(subTier);
                if (doColors)
                {
                    foreach (Match match in Regex.Matches(subVO.message, @"#([0-9a-fA-F]{6})"))
                    {
                        if (!string.IsNullOrEmpty(match.Value))
                            SetHexColor(match.Value);
                    }
                }
            }
        }

        private void HandleGiftSubAction(SubTiers subTier)
        {
            bool doColorLoop = false;
            bool doBlink = false;
            bool doBlinkLoop = false;

            if (subTier == SubTiers.Tier1)
            {
                doColorLoop = Properties.Settings.Default.giftSubT1.ToLower() == "loop";
                doBlink = Properties.Settings.Default.giftSubT1.ToLower() == "blink";
                doBlinkLoop = Properties.Settings.Default.giftSubT1.ToLower() == "blink+loop";
            }
            else if (subTier == SubTiers.Tier2)
            {
                doColorLoop = Properties.Settings.Default.giftSubT2.ToLower() == "loop";
                doBlink = Properties.Settings.Default.giftSubT2.ToLower() == "blink";
                doBlinkLoop = Properties.Settings.Default.giftSubT2.ToLower() == "blink+loop";
            }
            else if (subTier == SubTiers.Tier3)
            {
                doColorLoop = Properties.Settings.Default.giftSubT3.ToLower() == "loop";
                doBlink = Properties.Settings.Default.giftSubT3.ToLower() == "blink";
                doBlinkLoop = Properties.Settings.Default.giftSubT3.ToLower() == "blink+loop";
            }
            if (doColorLoop) DoColorLoop();
            if (doBlink) DoBlink();

            return;
        }

        private bool HandleSubAction(SubTiers subTier)
        {

            bool doColorLoop = false;
            bool doBlink = false;
            bool doBlinkLoop = false;
            bool doChangeColors = false;

            if (subTier == SubTiers.Tier1)
            {
                doColorLoop = Properties.Settings.Default.Tier1SubAction.ToLower() == "loop";
                doBlink = Properties.Settings.Default.Tier1SubAction.ToLower() == "blink";
                doBlinkLoop = Properties.Settings.Default.Tier1SubAction.ToLower() == "blink+loop";
                doChangeColors = Properties.Settings.Default.Tier1ChangeColor;
            }
            else if (subTier == SubTiers.Tier2)
            {
                doColorLoop = Properties.Settings.Default.Tier2SubAction.ToLower() == "loop";
                doBlink = Properties.Settings.Default.Tier2SubAction.ToLower() == "blink";
                doBlinkLoop = Properties.Settings.Default.Tier2SubAction.ToLower() == "blink+loop";
                doChangeColors = Properties.Settings.Default.Tier2ChangeColor;
            }
            else if (subTier == SubTiers.Tier3)
            {
                doColorLoop = Properties.Settings.Default.Tier3SubAction.ToLower() == "loop";
                doBlink = Properties.Settings.Default.Tier3SubAction.ToLower() == "blink";
                doBlinkLoop = Properties.Settings.Default.Tier3SubAction.ToLower() == "blink+loop";
                doChangeColors = Properties.Settings.Default.Tier3ChangeColor;
            }
            else if (subTier == SubTiers.Prime)
            {
                doColorLoop = Properties.Settings.Default.primeSubAction.ToLower() == "loop";
                doBlink = Properties.Settings.Default.primeSubAction.ToLower() == "blink";
                doBlinkLoop = Properties.Settings.Default.primeSubAction.ToLower() == "blink+loop";
                doChangeColors = Properties.Settings.Default.PrimeSubChangeColor;
            }

            if (doColorLoop) DoColorLoop();
            if (doBlink) DoBlink();
            if (doBlinkLoop) DoBlinkLoop();
            return doChangeColors;

        }

        private async void DoBlinkLoop()
        {
            if (await client.CheckConnection() == true)
            {
                var commandLoopOn = new LightCommand();
                commandLoopOn.Effect = Effect.ColorLoop;
                var commandLoopOff = new LightCommand();
                commandLoopOff.Effect = Effect.None;
                var commandBlink = new LightCommand();
                commandBlink.Alert = Alert.Multiple;

                Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                await client.SendCommandAsync(commandLoopOn, selectedGroup.Lights);
                await client.SendCommandAsync(commandBlink, selectedGroup.Lights);

                int loopDuration = Convert.ToInt32(Properties.Settings.Default.colorLoopDuration) * 1000;

                Thread.Sleep(loopDuration);
                await client.SendCommandAsync(commandLoopOff, selectedGroup.Lights);

                logEvent("Doing a Color Looping blink", true);
            }
        }

        private async void DoColorLoop()
        {
            if (await client.CheckConnection() == true)
            {
                var commandLoopOn = new LightCommand();
                commandLoopOn.Effect = Effect.ColorLoop;
                var commandLoopOff = new LightCommand();
                commandLoopOff.Effect = Effect.None;
                Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                await client.SendCommandAsync(commandLoopOn, selectedGroup.Lights);

                int loopDuration =Convert.ToInt32(Properties.Settings.Default.colorLoopDuration) * 1000;

                Thread.Sleep(loopDuration);
                await client.SendCommandAsync(commandLoopOff, selectedGroup.Lights);

                logEvent("Doing a Color Loop",true);
            }
        }

        private async void DoBlink()
        {
            if (await client.CheckConnection() == true)
            {
                var command = new LightCommand();
                command.Alert = Alert.Multiple;
                Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                await client.SendCommandAsync(command, selectedGroup.Lights);
                logEvent("Doing a Blink",true);
            }
        }

        private async void SetHexColor(string hex)
        {
            if (await client.CheckConnection() == true)
            {
                var command = new LightCommand();
                command.SetColor(new RGBColor(hex));
                Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                await client.SendCommandAsync(command, selectedGroup.Lights);
                logEvent("Setting Lights to " + hex,true);
            }
        }

        private void SetLightsOff()
        {
            var command = new LightCommand();
            command.TurnOff();
            Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();


            client.SendCommandAsync(command, selectedGroup.Lights);
        }

        private void SetLightsOn()
        {
            var command = new LightCommand();
            command.TurnOn();
            Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();


            client.SendCommandAsync(command, selectedGroup.Lights);
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

        }

        private Q42.HueApi.Models.Groups.Group getSelectedGroup()
        {
            Q42.HueApi.Models.Groups.Group selectedGroup = null;
            foreach (Q42.HueApi.Models.Groups.Group group in groups)
            {
                string g = "";
                Invoke(new Action(() => { g = comboBox6.SelectedItem.ToString(); }));
                Console.WriteLine(g);
                if (group.Name.Equals(g, StringComparison.Ordinal))
                {
                    selectedGroup = group;
                    break;
                }
            }
            return selectedGroup;
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            // Color Loop
            if (await client.CheckConnection() == true)
            {
                var commandLoopOn = new LightCommand();
                commandLoopOn.Effect = Effect.ColorLoop;
                var commandLoopOff = new LightCommand();
                commandLoopOff.Effect = Effect.None;
                Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                await client.SendCommandAsync(commandLoopOn, selectedGroup.Lights);


                new Thread(() =>
                {
                    Thread.Sleep(20000);
                    client.SendCommandAsync(commandLoopOff, selectedGroup.Lights);
                }).Start();
                logEvent("Looped Lights via UI",true);
            }
        }

        private async void button6_Click(object sender, EventArgs e)
        {
            // Blink
            if (await client.CheckConnection() == true)
            {
                var command = new LightCommand();
                command.Alert = Alert.Multiple;
                Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                await client.SendCommandAsync(command, selectedGroup.Lights);
                logEvent("Blinked Lights via UI",true);
            }
        }

        private async void button7_Click(object sender, EventArgs e)
        {
            // Custom Color
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                if (await client.CheckConnection() == true)
                {
                    var command = new LightCommand();
                    RGBColor color = new RGBColor();
                    color.R = colorDialog1.Color.R;
                    color.G = colorDialog1.Color.G;
                    color.B = colorDialog1.Color.B;
                    command.SetColor(color);
                    Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                    await client.SendCommandAsync(command, selectedGroup.Lights);
                    logEvent("Changed color via UI",true);
                }
            }
        }

        private async void button8_Click(object sender, EventArgs e)
        {
            // Lights On
            if (await client.CheckConnection() == true)
            {
                var command = new LightCommand();
                command.TurnOn();
                Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                await client.SendCommandAsync(command, selectedGroup.Lights);
                logEvent("Turned On Lights via UI",true);
            }
        }

        private async void button9_Click(object sender, EventArgs e)
        {
            // Lights Off
            if (await client.CheckConnection() == true)
            {
                var command = new LightCommand();
                command.TurnOff();
                Q42.HueApi.Models.Groups.Group selectedGroup = getSelectedGroup();

                await client.SendCommandAsync(command, selectedGroup.Lights);
                logEvent("Turned Off Lights via UI",true);
            }
        }

        private void eventLog1_EntryWritten(object sender, System.Diagnostics.EntryWrittenEventArgs e)
        {
            if (textBox6.InvokeRequired)
            {
                textBox6.Invoke(new Action(() => { textBox6.AppendText(e.Entry.Message + Environment.NewLine); }));
                textBox6.Update();
            }
            else
            {
                textBox6.AppendText(e.Entry.Message + Environment.NewLine);
                textBox6.Update();
            }
        }

        private void logEvent(string eventText)
        {
            eventLog1.WriteEntry(eventText);

#if DEBUG
            textBox6.Invoke(new Action(() => { textBox6.AppendText(eventText + Environment.NewLine); }));
#endif
        }

        private void logEvent(string eventText, bool writeToTextbox)
        {
            eventLog1.WriteEntry(eventText);

            if (writeToTextbox)
            {
                textBox6.Invoke(new Action(() => { textBox6.AppendText(eventText + Environment.NewLine); }));

            }

        }

        private void exitToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (pubsubThread != null)
            {
                redisPubSub.Stop();
            }
            Environment.Exit(0);
        }

        private void showHueLightBotToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.Visible == false)
            {
                this.ShowDialog();
            }
        }

        private void toolStripStatusLabel3_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://discord.gg/7KRch4J");
        }
    }

    public class RaidVO
    {
        public string type { get; set; }
        public int viewers { get; set; }
    }

    public class CheerVO
    {
        public string type { get; set; }
        public string nick { get; set; }
        public int amount { get; set; }
        public string message { get; set; }
    }

    public class SubVO
    {
        public string tier { get; set; }
        public string nick { get; set; }
        public bool gift { get; set; }
        public bool mysterygift { get; set; }
        public string message { get; set; }
        public string type { get; set; }
    }

    public class CommandVO
    {
        public string type { get; set; }
        public string nick { get; set; }
        public bool mod { get; set; }
        public bool sub { get; set; }
        public string message { get; set; }
    }

    public enum SubTiers
    {
        Unassigned = -1,
        Prime = 0,
        Tier1 = 1000,
        Tier2 = 2000,
        Tier3 = 3000,
    }
}