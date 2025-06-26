using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using MaterialSkin;
using MaterialSkin.Controls;
using Memory;
using WindowsInput;
using WindowsInput.Native;

namespace SWTOR_External
{
    public partial class Form1 : MaterialForm
    {
        private readonly Mem m = new Mem();
        private readonly Random rnd = new Random();
        private readonly InputSimulator sim = new InputSimulator();

        // Form Init
        public Form1()
        {
            InitializeComponent();
            var aobThread = new Thread(scanAOB) { IsBackground = true };
            var NumpadTeleportThread = new Thread(teleportNumpad) { IsBackground = true };
            var HotkeyThread = new Thread(hotkeysFunction) { IsBackground = true };
            // Thread pvpThread = new Thread(checkForPvP) { IsBackground = true , Priority = ThreadPriority.Lowest};
            // Design stuff
            MaterialSkinManager.Instance.ColorScheme = new ColorScheme(Primary.Green800, Primary.Green900,
                Primary.Green900, Accent.Green700, TextShade.WHITE);
            // online checks
            onlineCheck(urlRunning);
            updateCheck(urlUpdate);
            // holiday check
            holicheck();
            // start timers
            startMainTimer();
            startgetBaseTimer();
            // startPvPTimer();
            var title = rnd.Next(999999, 9999999);
            // this.Text = title.ToString("X2");
            var PID = m.GetProcIdFromName("swtor");
            log_console.Text += $"Welcome {userName}!\r\n";
            // Connect To Process
            if (PID != 0)
            {
                m.OpenProcess(PID);
                log_console.Text = log_console.Text + "\r\nConnected to PID: " + PID + "\r\n\r\nInitiliazing...\r\n";
            }
            else
            {
                MessageBox.Show("SWTOR must be running...");
                Environment.Exit(0);
            }

            // setVersionLabel
            lbl_version.Text = currentVersion;

            try
            {
                // load hotkeys from .dat file
                loadHotkeys();
                // load locationList .dat file
                loadLocations();
            }
            catch
            {
            }

            // patch on process exit
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            // start threads
            aobThread.Start();
            NumpadTeleportThread.Start();
            HotkeyThread.Start();
            Text = materialTabControl1.SelectedTab.Text;
            box_darkmode.Checked = true;
            // log_console.Text = log_console.Text + "\r\nPBase MemLoc: " + noclipAddress + "\r\n\r\n" + "Camera MemLoc: " + cameraAddress + "\r\n\r\n" + "CameraY MemLoc: " + cameraYAddress + "\r\n\r\n" + "Camera ZMemLoc: " + cameraZAddress;
        }

        #region InputFields

        private void box_playerHeight_TextChanged(object sender, EventArgs e)
        {
            try
            {
                // human error prevention
                if (box_playerHeight.Text == "")
                    return;
                if (box_playerHeight.Text.Contains(","))
                    box_playerHeight.Text = box_playerHeight.Text.Replace(",", ".");

                m.WriteMemory(heightAddrString, "float", $"{box_playerHeight.Text}");
            }
            catch
            {
                logToConsole("Invalid player height");
            }
        }

        #endregion

        // ICard for scripting
        public interface ICard
        {
            string GetCardInfo();
        }

        /* Todo:
        MouseTP (for map and world)
        */

        #region vars

        private bool moveAfterMsg;
        private bool msgSpamFlag;
        private bool enableSpam;
        private DateTime dateTimeBuffer = DateTime.Now;
        private bool antiAFK;
        private int rubberbandCount;
        private int creditClickerCount;
        private Vector3 lastPos;
        private bool darkmodeEnabled;
        private readonly string urlRunning = "https://github.com/NightfallChease/s/blob/main/isRunning.sw";
        private readonly string urlUpdate = "https://github.com/NightfallChease/s/blob/main/version9.2.1.sw";
        private readonly string currentVersion = "v9.2.1";
        private bool noclipPatched;
        private bool cameraPatched;
        private bool cameraZPatched;
        private bool cameraYPatched;
        private bool noCollisionEnabled;
        private bool noclipCave;
        private bool camCave;
        private bool freeCamEnabled;
        private bool flyModeEnabled;
        private bool attachToCamEnabled;
        private bool nofallEnabled;
        private bool nofallPatched;
        private bool alwaysOnTop;
        private bool devEspEnabled;
        private bool devVelEnabled;
        private string noAnimationAddrString;
        private bool noAnimationPatched;
        private bool infJumpPatched;
        private string infJumpAddrStr;
        private readonly string infJumpAOB = "F2 0F 11 47 0C 89 47 14 80";
        private readonly string stuckAOB = "66 00 CC CC CC CC CC CC CC CC 48 8B C4 55 56";
        private readonly string noAnimationAOB = "F3 0F 11 8B 70 02 00 00";
        private readonly string noclipAOB = "48 8B 01 48 8B 40 58 FF 15 ?? ?? ?? ?? 48 8B C8";
        private readonly string cameraAOB = "F3 0F 10 87 D0 03 00 00";
        private readonly string cameraYAOB = "89 87 10 02 00 00 F3";
        private readonly string cameraZAOB = "F2 0F 11 87 08 02 00 00";
        private readonly string nofallAOB = "F3 44 0F 10 4F 10 44 0F 28 DF";
        private readonly string speedHackAOB = "F3 0F 10 BE DC 00 00 00 0F 28 F7";
        private readonly string devESPAob = "0F 84 ?? ?? ?? ?? B9 06 00 00 00 41 FF D4 48 BE";
        private readonly string velocityIndicatorAOB =
            "74 1F B9 ?? ?? ?? ?? 41 FF D6 4C 8D 45 D0 B9 ?? ?? ?? ?? 48 89 F2 FF D7 48 8B 4D D0 FF D0 41 FF D7";
        private readonly string glideAOB = "F3 44 0F 11 43 14 F3 0F";
        private readonly string wallhackAOB = "74 0D 83 4B 68 01";
        private readonly string wallhack2AOB = "0F 84 76 02 00 00 49 8B CE";
        private readonly string infReachAOB = "67 FD FF 8B 06 89 07 41 80 0F 0C";
        private readonly string camCollisionAOB = "F3 0F 11 8F ?? ?? 00 00 0F B6 4F ??";
        private string camCollisionAddrStr;
        private bool camCollisionEnabled;
        private bool infReachEnabled;
        private bool infReachPatched;
        private string infReachAddressStr;
        private UIntPtr infReachAddress;

        private readonly byte[] infReachPatchedBytes =
        {
            0x81, 0xBC, 0x24, 0x3C, 0x01, 0x00, 0x00, 0xF4, 0x7D, 0x00, 0x00, 0x0F, 0x8C, 0x16, 0x00, 0x00, 0x00, 0x81,
            0xBC, 0x24, 0x3C, 0x01, 0x00, 0x00, 0x2C, 0x7E, 0x00, 0x00, 0x0F, 0x87, 0x05, 0x00, 0x00, 0x00, 0xB8, 0x00,
            0x00, 0x00, 0x00, 0x89, 0x07, 0x41, 0x80, 0x0F, 0x0C
        };

        private string wallhackAddress;
        private string wallhack2Address;
        private bool wallhackPatched;
        private bool wallhack2Patched;
        private bool glideEnabled;
        private VirtualKeyCode infJumpKey = VirtualKeyCode.SPACE;
        private VirtualKeyCode TPUpKey;
        private VirtualKeyCode TPDownKey;
        private VirtualKeyCode TPLeftKey;
        private VirtualKeyCode TPRightKey;
        private VirtualKeyCode TPForwardKey;
        private VirtualKeyCode TPBackwardKey;
        private VirtualKeyCode FreecamKey;
        private VirtualKeyCode TPToCamKey;
        private VirtualKeyCode NofallKey;
        private VirtualKeyCode GlideKey;
        private VirtualKeyCode SpeedKey;
        private VirtualKeyCode forwardsKey;
        private VirtualKeyCode backwardsKey;
        private VirtualKeyCode flyUpKey;
        private VirtualKeyCode flyDownKey;
        private VirtualKeyCode enableSpamKey;
        public string PlayerBaseAddress = "";
        public string CamBaseAddress = "";
        public UIntPtr playerBaseUInt;
        public UIntPtr camBaseUInt;
        public UIntPtr EspUint;
        public int baseAddr;
        public UIntPtr noclipAddress;
        public string noclipAddressStr = "";
        public string glideAddrString;
        public string cameraAddress = "";
        public string cameraYAddress = "";
        public string cameraZAddress = "";
        public float xCoord;
        public float yCoord;
        public float zCoord;
        private UIntPtr floorYAddr;
        private string floorYAddrStr;
        private float floorYValue;
        public float camSpeed = 0.04f;
        public float speedBoostMultiplier = 2f;
        public bool isSpeedBoostActive;
        public string xAddrString = "";
        public string yAddrString = "";
        public string zAddrString = "";
        public string devESPAddrString = "";
        public string velocityIndAddrStr = "";
        public string xCamAddrString = "";
        public string yCamAddrString = "";
        public string zCamAddrString = "";
        public string nofallAddrString = "";
        public string speedValueUIntString;
        public string speedHackAddrString;
        public string yawAddrString;
        public string pitchAddrString;
        private string wFloorAddrStr;
        private float walkableFloor;
        public float savedX;
        public float savedY;
        public float savedZ;
        public UIntPtr xAddr;
        public UIntPtr yAddr;
        public UIntPtr zAddr;
        public UIntPtr xCamAddr;
        public UIntPtr yCamAddr;
        public UIntPtr zCamAddr;
        public UIntPtr nofallAddr;
        public UIntPtr pitchAddr;
        public UIntPtr yawAddr;
        public UIntPtr cameraYUInt;
        public UIntPtr cameraZUInt;
        public UIntPtr heightAddr;
        public UIntPtr movementModeAddr;
        public float playerXVelocity;
        public float playerYVelocity;
        public float playerZVelocity;
        public int throwbackValue;
        public float throwSavedX;
        public float throwSavedY;
        public float throwSavedZ;
        public bool throwBackSaved;
        public DateTime lastThrowbackTime = DateTime.Now;
        public string movementModeAddrStr;
        public string heightAddrString;
        public bool tpflag;
        public bool saveflag;
        public bool speedHackCave;
        public float playerHeight;
        private readonly string userName = Environment.UserName;
        private UIntPtr speedValueUInt;
        private byte[] speedPatchedBytes;
        private bool speedPatched;

        private UIntPtr pbasecaveAddr;

        // private bool isPVPEnabled = false;
        private string PbaseUintString;

        // private string PVPAOB = "50 00 56 00 ?? 00 00 00 ?? ?? ?? ?? ?? ?? 00 00";
        // private string PVPAOB = "50 00 56 00 ?? 00 00 00 ?? ?? ?? ?? ?? 7D 00 00 ?? ?? ?? ?? ??";
        // private string pvpAddrStr;
        // private UIntPtr pvpAddr;
        private bool isSpeedhackEnabled;
        public byte[] noclipPatchedBytes = { };
        public byte[] cameraPatchedBytes = { };
        public byte[] cameraYPatchedBytes = { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };
        public byte[] cameraZPatchedBytes = { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };
        public byte[] noclipBytes = { 0x48, 0x8B, 0x01, 0x48, 0x8B, 0x40, 0x58 };
        public byte[] cameraBytes = { 0xF3, 0x0F, 0x10, 0x87, 0xD0, 0x03, 0x00, 0x00 };
        public byte[] speedBytes = { 0xF3, 0x0F, 0x10, 0xBE, 0xDC, 0x00, 0x00, 0x00, 0x0F, 0x28, 0xF7 };
        public byte[] cameraYBytes = { 0x89, 0x87, 0x10, 0x02, 0x00, 0x00, 0xF3 };
        public byte[] cameraZBytes = { 0xF2, 0x0F, 0x11, 0x87, 0x08, 0x02, 0x00, 0x00 };
        public byte[] patchedBytes = { 0xC7, 0x47, 0x10, 0x33, 0x33, 0x33, 0xBF, 0xF3, 0x44, 0x0F, 0x10, 0x4F, 0x10 };
        public byte[] originalBytes = { 0xF3, 0x44, 0x0F, 0x10, 0x4F, 0x10 };
        public byte[] gotoCaveBytes = { };
        public byte[] infReachAlreadyPatchedBytes = { };

        public byte[] infReachOriginalBytes = { 0x89, 0x07, 0x41, 0x80, 0x0F, 0x0C };

        // private bool stuckPatched = false;
        private string stuckAddrStr;
        private UIntPtr stuckAddrUint;
        private List<customLocation> locationList;
        private bool isInfJumpToggled;
        private bool noKnockbackEnabled;
        private bool isInfJumpActivated;

        #endregion

        #region Timer

        // //private void pvpTimer_Tick(object sender, EventArgs e)
        // //{
        // //    pvpAddrStr = pvpAddrStr ?? "00";
        // //    string isPvPEnabledAddr = convertUintToHexString(pvpAddr + 0x4);
        // //    if (pvpAddrStr != "00")
        //  {
        //      int pvpByte = m.ReadByte(isPvPEnabledAddr);
        //      if (pvpByte != 0x45)
        //      {
        //          pvpTimer.Stop();
        //          cbox_masterSwitch.Checked = false;
        //          MessageBox.Show("PvP detected!\nPlease disable PvP to continue using the tool");
        //          Environment.Exit(0);
        //      }
        //  }
        // //}
        private void mainTimer_Tick_1(object sender, EventArgs e)
        {
            // AntiDebug
            // PreventProgramFromBeingDebuged();

            if (PbaseUintString != null)
            {
                xAddr = playerBaseUInt + 0x68;
                xAddrString = convertUintToHexString(xAddr);

                yAddr = playerBaseUInt + 0x6C;
                yAddrString = convertUintToHexString(yAddr);

                zAddr = playerBaseUInt + 0x70;
                zAddrString = convertUintToHexString(zAddr);

                zCamAddr = camBaseUInt + 0x210;
                zCamAddrString = convertUintToHexString(zCamAddr);

                yCamAddr = camBaseUInt + 0x20C;
                yCamAddrString = convertUintToHexString(yCamAddr);

                xCamAddr = camBaseUInt + 0x208;
                xCamAddrString = convertUintToHexString(xCamAddr);

                pitchAddr = camBaseUInt + 0x290;
                pitchAddrString = convertUintToHexString(pitchAddr);

                movementModeAddr = playerBaseUInt + 0x390;
                movementModeAddrStr = convertUintToHexString(movementModeAddr);

                yawAddr = camBaseUInt + 0x218;
                yawAddrString = convertUintToHexString(yawAddr);

                heightAddr = playerBaseUInt + 0x84;
                heightAddrString = convertUintToHexString(heightAddr);

                floorYAddr = playerBaseUInt + 0x348;
                floorYAddrStr = convertUintToHexString(floorYAddr);

                var wFloorAddr = m.Get64BitCode($"{PbaseUintString},0x330,0x2E0");
                wFloorAddrStr = convertUintToHexString(wFloorAddr);
                walkableFloor = m.ReadFloat(wFloorAddrStr);

                var velocityBaseAddr = m.Get64BitCode($"{PbaseUintString},0x470");
                var velocityBaseAddrStr = convertUintToHexString(velocityBaseAddr);

                playerXVelocity = m.ReadFloat($"{velocityBaseAddrStr},0x0C");
                playerYVelocity = m.ReadFloat($"{velocityBaseAddrStr},0x10");
                playerZVelocity = m.ReadFloat($"{velocityBaseAddrStr},0x14");

                throwbackValue = m.ReadInt($"{PbaseUintString},0x64C");

                xCoord = m.ReadFloat(xAddrString);
                yCoord = m.ReadFloat(yAddrString);
                zCoord = m.ReadFloat(zAddrString);

                floorYValue = m.ReadFloat(floorYAddrStr);
                playerHeight = m.ReadFloat(heightAddrString);

                lbl_coords.Text = $"X: {xCoord}\nY: {yCoord}\nZ: {zCoord}";
                lbl_savedCoords.Text = $"X: {savedX}\nY: {savedY}\nZ: {savedZ}";
                lbl_yFloorValue.Text = $"FloorY: {floorYValue}";

                if (tpflag) teleport();

                if (freeCamEnabled) Freecam();

                if (flyModeEnabled) FlyMode();

                if (noKnockbackEnabled)
                {
                    if (throwbackValue == 256)
                    {
                        // Update the last time throwbackValue was 256
                        lastThrowbackTime = DateTime.Now;

                        Thread.Sleep(100);

                        if (!throwBackSaved)
                        {
                            throwSavedX = xCoord;
                            throwSavedY = yCoord;
                            throwSavedZ = zCoord;
                            throwBackSaved = true;
                        }

                        m.WriteMemory(xAddrString, "float", throwSavedX.ToString(CultureInfo.InvariantCulture));
                        m.WriteMemory(yAddrString, "float", throwSavedY.ToString(CultureInfo.InvariantCulture));
                        m.WriteMemory(zAddrString, "float", throwSavedZ.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        // Check if three seconds have passed since the last time throwbackValue was 256
                        if ((DateTime.Now - lastThrowbackTime).TotalSeconds >= 3)
                        {
                            // Reset throwSaved values and throwBackSaved flag
                            throwSavedX = 0;
                            throwSavedY = 0;
                            throwSavedZ = 0;
                            throwBackSaved = false;
                        }
                    }
                }

                if (antiAFK) antiAFKfunc();

                if (enableSpam)
                {
                    lbl_chatspamstatus.Text = "On";
                    lbl_chatspamstatus.ForeColor = Color.Green;
                }
                else
                {
                    lbl_chatspamstatus.Text = "Off";
                    lbl_chatspamstatus.ForeColor = Color.Red;
                }
            }
        }

        private void timer_getBase_Tick(object sender, EventArgs e)
        {
            try
            {
                // SpeedhackManagement
                if (isSpeedhackEnabled)
                    m.WriteMemory(speedValueUIntString, "float",
                        trckbr_speed.Value.ToString(CultureInfo.InvariantCulture));

                // check if speedhack is disabled
                if (!isSpeedhackEnabled) m.WriteMemory(speedValueUIntString, "float", "0");

                // //PBASE
                var caveAddrString = convertUintToHexString(pbasecaveAddr);
                // log_console.Text = log_console.Text + "\r\n\r\nCaveAddr = " + caveAddrString;

                // Add offset 0x12 to the cwave addr (that's where the ptr for pbase is stored)
                playerBaseUInt = UIntPtr.Add(pbasecaveAddr, 0x20); //caveAddr == UIntPtr

                // log caveAddr + offset
                PbaseUintString = convertUintToHexString(playerBaseUInt);
                // log_console.Text = log_console.Text + "\r\n\r\nAddress of the PTR = " + PbaseUintString;

                // readValue & convert to hex string
                var playerBaselong = m.ReadLong(PbaseUintString);
                PlayerBaseAddress = playerBaselong.ToString("X2",CultureInfo.InvariantCulture);
                playerBaseUInt = ParseHexToUIntPtr(PlayerBaseAddress);
            }
            catch
            {
            }
        }

        #endregion

        #region Checkboxes

        private void box_chatSpamMove_CheckedChanged(object sender, EventArgs e)
        {
            moveAfterMsg = !moveAfterMsg;
        }

        private void box_chatSpammer_CheckedChanged(object sender, EventArgs e)
        {
            var msgSpammerThread = new Thread(chatSpamLoop) { IsBackground = true };
            msgSpamFlag = !msgSpamFlag;

            if (msgSpamFlag) msgSpammerThread.Start();
        }

        private void box_antiAfk_CheckedChanged(object sender, EventArgs e)
        {
            if (!antiAFK)
                MessageBox.Show("Make sure you are tabbed into the game", "Quick tip", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

            antiAFK = !antiAFK;
            dateTimeBuffer = DateTime.Now;
        }

        private void box_infJump_CheckedChanged(object sender, EventArgs e)
        {
            isInfJumpActivated = !isInfJumpActivated;
        }

        private void box_flyMode_CheckedChanged(object sender, EventArgs e)
        {
            toggle_FlyMode();

            // patchStuck();

            if (flyModeEnabled)
            {
                box_noCollision.Checked = true;
                //box_noCamCollision.Checked = true;
            }
            else
            {
                box_noCollision.Checked = false;
                //box_noCamCollision.Checked = false;
            }
        }

        private void box_noAnimations_CheckedChanged(object sender, EventArgs e)
        {
            if (!noAnimationPatched)
            {
                m.WriteMemory(noAnimationAddrString, "bytes", "90 90 90 90 90 90 90 90");
                noAnimationPatched = true;
            }
            else
            {
                m.WriteMemory(noAnimationAddrString, "bytes", "F3 0F 11 8B 70 02 00 00");
                noAnimationPatched = false;
            }
        }

        private void box_infReach_CheckedChanged(object sender, EventArgs e)
        {
            infReachFunction();
        }

        private void box_noCollision_CheckedChanged(object sender, EventArgs e)
        {
            toggleNoCollision();
        }

        private void box_darkMode_CheckedChanged(object sender, EventArgs e)
        {
            if (!darkmodeEnabled)
            {
                var fColor = Color.FromArgb(220, 220, 220);
                var bColor = Color.FromArgb(50, 50, 50);
                MaterialSkinManager.Instance.ColorScheme = new ColorScheme(Primary.Green800, Primary.Green900,
                    Primary.Green900, Accent.Green700, TextShade.BLACK);
                MaterialSkinManager.Instance.Theme = MaterialSkinManager.Themes.DARK;
                BackColor = bColor;
                ForeColor = fColor;

                tabPage1.BackColor = bColor;
                tabPage1.ForeColor = fColor;
                tabPage2.BackColor = bColor;
                tabPage2.ForeColor = fColor;
                tabPage4.BackColor = bColor;
                tabPage4.ForeColor = fColor;
                tabPage5.BackColor = bColor;
                tabPage5.ForeColor = fColor;
                tabPage6.ForeColor = fColor;
                tabPage6.BackColor = bColor;
                trckbr_speed.BackColor = bColor;
                listbox_teleportLocations.BackColor = bColor;
                listbox_teleportLocations.ForeColor = fColor;

                SetControlColors(Controls, bColor, fColor);

                darkmodeEnabled = true;
            }
            else
            {
                MaterialSkinManager.Instance.ColorScheme = new ColorScheme(Primary.Green800, Primary.Green900,
                    Primary.Green900, Accent.Green700, TextShade.WHITE);
                MaterialSkinManager.Instance.Theme = MaterialSkinManager.Themes.LIGHT;
                BackColor = Color.FromArgb(255, 255, 255);
                ForeColor = Color.FromArgb(0, 0, 0);
                var fColor = Color.FromArgb(0, 0, 0);
                var bColor = Color.FromArgb(255, 255, 255);
                tabPage1.BackColor = bColor;
                tabPage1.ForeColor = fColor;
                tabPage2.BackColor = bColor;
                tabPage2.ForeColor = fColor;
                tabPage4.BackColor = bColor;
                tabPage4.ForeColor = fColor;
                tabPage5.BackColor = bColor;
                tabPage5.ForeColor = fColor;
                trckbr_speed.BackColor = bColor;
                trckbr_speed.BackColor = fColor;
                listbox_teleportLocations.BackColor = bColor;
                listbox_teleportLocations.ForeColor = fColor;

                SetControlColors(Controls, Color.FromArgb(250, 250, 250), Color.FromArgb(0, 0, 0));

                darkmodeEnabled = false;
            }
        }

        private void cbox_masterSwitch_CheckedChanged_1(object sender, EventArgs e)
        {
            var codeCaveThread = new Thread(createCodeCave);

            if (cbox_masterSwitch.ForeColor != Color.Green)
            {
                Cursor.Current = Cursors.WaitCursor;
                cbox_masterSwitch.ForeColor = Color.Green;
            }
            else
            {
                cbox_masterSwitch.ForeColor = Color.Red;
            }
            // MessageBox.Show("Try to not disable this function or it may cause errors");

            codeCaveThread.Start();
        }

        private void box_Freecam_CheckedChanged(object sender, EventArgs e)
        {
            toggle_freecam();
        }

        private void box_nofall_CheckedChanged(object sender, EventArgs e)
        {
            nofallFunction();
        }

        private void box_camAttach_CheckedChanged(object sender, EventArgs e)
        {
            if (!attachToCamEnabled)
                attachToCamEnabled = true;
            else
                attachToCamEnabled = false;
        }

        private void box_alwaysInFront_CheckedChanged(object sender, EventArgs e)
        {
            if (!alwaysOnTop)
            {
                TopMost = true;
                alwaysOnTop = true;
            }
            else
            {
                TopMost = false;
                alwaysOnTop = false;
            }
        }

        private void box_esp_CheckedChanged(object sender, EventArgs e)
        {
            if (!devEspEnabled)
            {
                m.WriteMemory($"{devESPAddrString}", "bytes", "0F 85");
                devEspEnabled = true;
            }
            else
            {
                m.WriteMemory($"{devESPAddrString}", "bytes", "0F 84");
                devEspEnabled = false;
            }
        }

        private void box_dotEsp_CheckedChanged(object sender, EventArgs e)
        {
            if (!devVelEnabled)
            {
                m.WriteMemory($"{velocityIndAddrStr}", "bytes", "75 1F B9");
                devVelEnabled = true;
            }
            else
            {
                m.WriteMemory($"{velocityIndAddrStr}", "bytes", "74 1F B9");
                devVelEnabled = false;
            }
        }

        private void box_wallhack_CheckedChanged(object sender, EventArgs e)
        {
            // first patch
            if (!wallhackPatched)
            {
                m.WriteMemory($"{wallhackAddress}", "bytes", "90 90");
                wallhackPatched = true;
            }
            else
            {
                m.WriteMemory($"{wallhackAddress}", "bytes", "74 0D");
                wallhackPatched = false;
            }

            // second patch
            if (!wallhack2Patched)
            {
                m.WriteMemory($"{wallhack2Address}", "bytes", "90 90");
                wallhack2Patched = true;
            }
            else
            {
                m.WriteMemory($"{wallhack2Address}", "bytes", "0F 84");
                wallhack2Patched = false;
            }
        }

        private void box_glide_CheckedChanged(object sender, EventArgs e)
        {
            doglide();
        }

        private void box_speedhack_CheckedChanged(object sender, EventArgs e)
        {
            speedhackFunction();
        }

        private void box_noCamCollision_CheckedChanged(object sender, EventArgs e)
        {
            toggle_camCollision();
        }

        private void box_noKnockback_CheckedChanged(object sender, EventArgs e)
        {
            noKnockbackEnabled = !noKnockbackEnabled;
        }

        #endregion

        #region Functions

        private void holicheck()
        {
            var today = DateTime.Today;
            var emoji = "";

            // Check for specific holidays
            if (today.Month == 4 && IsEasterSunday(today)) // Easter (approximated)
                emoji = " (Happy 4/20) 🐣 🥦";
            else if (today.Month == 12 && today.Day == 25) // Christmas
                emoji = "🎄";
            else if (today.Month == 10 && today.Day == 31) // Halloween
                emoji = "🎃";
            // Add more holidays as needed

            // Append emoji to lbl_title if a holiday is detected
            if (!string.IsNullOrEmpty(emoji)) lbl_title.Text = lbl_title.Text + " " + emoji;
        }

        // Helper method to approximate Easter Sunday
        private bool IsEasterSunday(DateTime date)
        {
            // Simplified Easter calculation (Western Christian Easter)
            // Uses the Anonymous Gregorian algorithm for approximation
            var year = date.Year;
            var a = year % 19;
            var b = year / 100;
            var c = year % 100;
            var d = b / 4;
            var e = b % 4;
            var f = (b + 8) / 25;
            var g = (b - f + 1) / 3;
            var h = (19 * a + b - d - g + 15) % 30;
            var i = c / 4;
            var k = c % 4;
            var l = (32 + 2 * e + 2 * i - h - k) % 7;
            var m = (a + 11 * h + 22 * l) / 451;

            var month = (h + l - 7 * m + 114) / 31;
            var day = (h + l - 7 * m + 114) % 31 + 1;

            return date.Month == month && date.Day == day;
        }

        private void chatSpamLoop()
        {
            while (true)
                if (enableSpam)
                {
                    if (!box_chatSpammer.Checked) break;

                    sim.Keyboard.KeyPress(VirtualKeyCode.RETURN); // go into chat
                    Thread.Sleep(100);

                    foreach (var c in txtbox_chatSpammer.Text) // type every char of string
                    {
                        sim.Keyboard.TextEntry(c);
                        Thread.Sleep(10);
                    }

                    sim.Keyboard.KeyPress(VirtualKeyCode.RETURN); // send message

                    if (moveAfterMsg)
                    {
                        Thread.Sleep(50);
                        sim.Keyboard.KeyDown(VirtualKeyCode.VK_W);
                        Thread.Sleep(50);
                        sim.Keyboard.KeyUp(VirtualKeyCode.VK_W);
                    }

                    try
                    {
                        Thread.Sleep(int.Parse(txtbox_chatSpamDelay.Text));
                    }
                    catch
                    {
                        Thread.Sleep(2000);
                    }
                }
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            try
            {
                restoreOriginalCode();
            }
            catch
            {
                MessageBox.Show("Restoring code failed! Please restart the game.");
            }
        }

        private void infJumpPatch()
        {
            if (!infJumpPatched)
            {
                infJumpPatched = true;

                m.WriteMemory(infJumpAddrStr, "bytes", "90 90 90 90 90");
            }
            else
            {
                infJumpPatched = false;
                m.WriteMemory(infJumpAddrStr, "bytes", "F2 0F 11 47 0C");
            }
        }

        // private void patchStuck()
        // {
        //    //add additional bytes to scanned location because the AOB starts earlier than the target location
        //    UIntPtr addressToPatch = (stuckAddrUint + 0x0A);
        //    string addressToPatchStr = convertUintToHexString(addressToPatch);
        //
        //    if (!stuckPatched)
        //    {
        //        m.WriteMemory(addressToPatchStr, "bytes", "C3 90 90");
        //        stuckPatched = true;
        //    }
        //    else
        //    {
        //        m.WriteMemory(addressToPatchStr, "bytes", "48 8B C4");
        //        stuckPatched = false;
        //    }
        // }
        private void toggleNoCollision()
        {
            if (!noCollisionEnabled)
            {
                m.WriteMemory($"{movementModeAddrStr}", "int", "6");
                noCollisionEnabled = true;
            }
            else
            {
                m.WriteMemory($"{movementModeAddrStr}", "int", "1");
                noCollisionEnabled = false;
            }
        }

        private void toggle_camCollision()
        {
            if (!camCollisionEnabled)
            {
                camCollisionEnabled = true;

                m.WriteMemory(camCollisionAddrStr, "bytes", "90 90 90 90 90 90 90 90");
            }
            else
            {
                camCollisionEnabled = false;
                m.WriteMemory(camCollisionAddrStr, "bytes", "F3 0F 11 8F 50 03 00 00");
            }
        }

        private void loadHotkeys()
        {
            if (File.Exists("hotkeys.dat"))
                using (var fs = new FileStream("hotkeys.dat", FileMode.Open))
                {
                    var formatter = new BinaryFormatter();
                    var settings = (HotkeySettings)formatter.Deserialize(fs);

                    TPUpKey = settings.TPUpKey;
                    TPDownKey = settings.TPDownKey;
                    TPLeftKey = settings.TPLeftKey;
                    TPRightKey = settings.TPRightKey;
                    TPForwardKey = settings.TPForwardKey;
                    TPBackwardKey = settings.TPBackwardKey;
                    FreecamKey = settings.FreecamKey;
                    TPToCamKey = settings.TPToCamKey;
                    NofallKey = settings.NofallKey;
                    GlideKey = settings.GlideKey;
                    SpeedKey = settings.SpeedKey;
                    forwardsKey = settings.flyforwardsKey;
                    backwardsKey = settings.flybackwardsKey;
                    flyDownKey = settings.flyDownKey;
                    flyUpKey = settings.flyupKey;
                    enableSpamKey = settings.enableSpamKey;

                    txtbox_TPUpKey.Text = TPUpKey.ToString();
                    txtbox_TPDowNkey.Text = TPDownKey.ToString();
                    txtbox_TPLeftKey.Text = TPLeftKey.ToString();
                    txtbox_TPRightKey.Text = TPRightKey.ToString();
                    txtbox_TPForwardKey.Text = TPForwardKey.ToString();
                    txtbox_TPBackwardKey.Text = TPBackwardKey.ToString();
                    txtbox_freecamKey.Text = FreecamKey.ToString();
                    txtbox_tpToCamKey.Text = TPToCamKey.ToString();
                    txtbox_nofallKey.Text = NofallKey.ToString();
                    txtbox_glideKey.Text = GlideKey.ToString();
                    txtbox_speedKey.Text = SpeedKey.ToString();
                    txtbox_flyforwardskey.Text = forwardsKey.ToString();
                    txtbox_flybackwardskey.Text = backwardsKey.ToString();
                    txtbox_flyDown.Text = flyDownKey.ToString();
                    txtbox_flyUp.Text = flyUpKey.ToString();
                    txt_hotkeySpam.Text = enableSpamKey.ToString();
                }
            else
                logToConsole("No hotkey settings found.\r\n");
        }

        private void SetControlColors(Control.ControlCollection controls, Color backColor, Color foreColor)
        {
            foreach (Control control in controls)
                if (control is Button || control is TextBox || control is TrackBar)
                {
                    control.BackColor = backColor;
                    control.ForeColor = foreColor;
                }
                else if (control.HasChildren)
                {
                    SetControlColors(control.Controls, backColor, foreColor);
                }
        }

        private void toggle_freecam()
        {
            if (!cameraYPatched)
            {
                m.WriteBytes(cameraYUInt, cameraYPatchedBytes);
                // log_console.Text = log_console.Text + "\r\n\r\nCamYBytes Restored";
                cameraYPatched = true;
            }
            else
            {
                m.WriteBytes(cameraYUInt, cameraYBytes);
                // log_console.Text = log_console.Text + "\r\n\r\nCamYBytes Patched";
                cameraYPatched = false;
            }

            if (!cameraZPatched)
            {
                m.WriteBytes(cameraZUInt, cameraZPatchedBytes);
                // log_console.Text = log_console.Text + "\r\n\r\nCamXBytes Restored";
                cameraZPatched = true;
            }
            else
            {
                m.WriteBytes(cameraZUInt, cameraZBytes);
                cameraZPatched = false;
            }
            // Dont touch code above, important for freecam

            if (!freeCamEnabled)
                freeCamEnabled = true;
            else
                freeCamEnabled = false;
        }

        private void toggle_FlyMode()
        {
            if (!flyModeEnabled)
                flyModeEnabled = true;
            else
                flyModeEnabled = false;
        }

        private void scanAOB()
        {
            try
            {
                // PvP Scan
                // long startingAddr = 0x000000000000;
                // long endingAddr = 0x7fffffffffff;
                // pvpAddrStr = m.AoBScan(startingAddr, endingAddr, PVPAOB, true, false).Result.Sum().ToString("X2");
                // pvpAddr = m.Get64BitCode(pvpAddrStr);
                // if (pvpAddrStr == "00")
                // {
                //    MessageBox.Show("Please start the tool, once you are ingame");
                //    Environment.Exit(0);
                // }

                // AOB Scans
                var moduleStart = m.GetModuleAddressByName("swtor.exe"); //get start of module
                var moduleStartLong = long.Parse(moduleStart.ToString()); //parse it to long
                var moduleEndLong = moduleStartLong + 10000000;

                infJumpAddrStr = m.AoBScan(infJumpAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                noclipAddressStr = m.AoBScan(noclipAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                cameraAddress = m.AoBScan(moduleStartLong, moduleEndLong, cameraAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                cameraZAddress = m.AoBScan(cameraZAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                cameraYAddress = m.AoBScan(cameraYAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                nofallAddrString = m.AoBScan(nofallAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                speedHackAddrString = m.AoBScan(speedHackAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                devESPAddrString = m.AoBScan(devESPAob).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                velocityIndAddrStr = m.AoBScan(velocityIndicatorAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                glideAddrString = m.AoBScan(glideAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                wallhackAddress = m.AoBScan(wallhackAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                wallhack2Address = m.AoBScan(wallhack2AOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                infReachAddressStr = m.AoBScan(infReachAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                camCollisionAddrStr = m.AoBScan(camCollisionAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                noAnimationAddrString = m.AoBScan(noAnimationAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);
                stuckAddrStr = m.AoBScan(stuckAOB).Result.Sum().ToString("X2", CultureInfo.InvariantCulture);

                stuckAddrUint = m.Get64BitCode(stuckAddrStr);
                cameraYUInt = m.Get64BitCode(cameraYAddress);
                cameraZUInt = m.Get64BitCode(cameraZAddress);

                // fixInfReachAddr to +5 bytes
                infReachAddress = m.Get64BitCode(infReachAddressStr);
                infReachAddress += 0x5;
                infReachAddressStr = convertUintToHexString(infReachAddress);

                // fixPbaseAddr to +4 bytes
                noclipAddress = m.Get64BitCode(noclipAddressStr);
                noclipAddressStr = convertUintToHexString(noclipAddress);

                log_console.Invoke((MethodInvoker)delegate
                {
                    log_console.Text += "\r\nInitialization success";
                    cbox_masterSwitch.Enabled = true;
                });

                // MessageBox.Show("AOB scan success");
            }
            catch (Exception ex)
            {
                // log_console.Text = log_console.Text + $"\r\n\r\nAOB's not found. Please restart the game";
                MessageBox.Show($"AOB's not found. Please restart the game and the tool.\nError: {ex.Message}");
            }
        }

        private void Freecam()
        {
            float pitch = m.ReadFloat(pitchAddrString);
            float yaw = m.ReadFloat(yawAddrString);

            float siny = (float)Math.Sin(yaw);
            float cosy = (float)Math.Cos(yaw);
            float sinp = (float)Math.Sin(pitch);
            float cosp = (float)Math.Cos(pitch);

            float camx = m.ReadFloat(xCamAddrString);
            float camy = m.ReadFloat(yCamAddrString);
            float camz = m.ReadFloat(zCamAddrString);

            float speedX = 0;
            float speedY = 0;
            float speedZ = 0;

            float speed = camSpeed;

            if (isSpeedBoostActive) speed *= speedBoostMultiplier;

            bool isArrowUpPressed = sim.InputDeviceState.IsHardwareKeyDown(forwardsKey);
            bool isArrowDownPressed = sim.InputDeviceState.IsHardwareKeyDown(backwardsKey);

            if (isArrowUpPressed)
            {
                speedX = -speed * cosp * siny;
                speedZ = -speed * cosp * cosy;
                speedY = -speed * sinp; // Adjusted to move correctly with pitch
            }
            else if (isArrowDownPressed)
            {
                speedX = speed * cosp * siny;
                speedZ = speed * cosp * cosy;
                speedY = speed * sinp; // Adjusted to move correctly with pitch
            }

            if (sim.InputDeviceState.IsHardwareKeyDown(VirtualKeyCode.LEFT) ||
                sim.InputDeviceState.IsHardwareKeyDown(VirtualKeyCode.RIGHT))
                isSpeedBoostActive = true;
            else
                isSpeedBoostActive = false;

            if (sim.InputDeviceState.IsHardwareKeyDown(flyUpKey))
                speedY += speed * 0.2f;
            else if (sim.InputDeviceState.IsHardwareKeyDown(flyDownKey)) speedY -= speed * 0.2f;

            camx += speedX;
            camy += speedY;
            camz += speedZ;

            // Ensure the float values are converted to strings using the invariant culture
            string camxString = camx.ToString(CultureInfo.InvariantCulture);
            string camyString = camy.ToString(CultureInfo.InvariantCulture);
            string camzString = camz.ToString(CultureInfo.InvariantCulture);

            // Write the memory
            m.WriteMemory(xCamAddrString, "float", camxString);
            m.WriteMemory(yCamAddrString, "float", camyString);
            m.WriteMemory(zCamAddrString, "float", camzString);

            if (attachToCamEnabled)
            {
                savedX = camx;
                savedY = camy;
                savedZ = camz;
                tpflag = true;
            }
        }

        private void FlyMode()
        {
            // based on the idea of jrazorx ;)

            var valueManipulated = false;
            var pitch = m.ReadFloat(pitchAddrString);
            var yaw = m.ReadFloat(yawAddrString);

            var siny = (float)Math.Sin(yaw);
            var cosy = (float)Math.Cos(yaw);
            var sinp = (float)Math.Sin(pitch);
            var cosp = (float)Math.Cos(pitch);

            var playerX = m.ReadFloat(xAddrString);
            var playerY = m.ReadFloat(yAddrString);
            var playerZ = m.ReadFloat(zAddrString);

            float speedX = 0;
            float speedY = 0;
            float speedZ = 0;

            var speed = camSpeed;

            if (isSpeedBoostActive) speed *= speedBoostMultiplier;

            var isArrowUpPressed = sim.InputDeviceState.IsHardwareKeyDown(forwardsKey);
            var isArrowDownPressed = sim.InputDeviceState.IsHardwareKeyDown(backwardsKey);

            if (isArrowUpPressed)
            {
                speedX = -speed * cosp * siny;
                speedZ = -speed * cosp * cosy;
                speedY = -speed * sinp; // Adjusted to move correctly with pitch
                valueManipulated = true;
            }
            else if (isArrowDownPressed)
            {
                speedX = speed * cosp * siny;
                speedZ = speed * cosp * cosy;
                speedY = speed * sinp; // Adjusted to move correctly with pitch
                valueManipulated = true;
            }

            if (sim.InputDeviceState.IsHardwareKeyDown(VirtualKeyCode.RIGHT))
                isSpeedBoostActive = true;
            else
                isSpeedBoostActive = false;

            if (sim.InputDeviceState.IsHardwareKeyDown(flyUpKey))
            {
                speedY += speed * 0.2f;
                valueManipulated = true;
            }
            else if (sim.InputDeviceState.IsHardwareKeyDown(flyDownKey))
            {
                speedY -= speed * 0.2f;
                valueManipulated = true;
            }

            playerX += speedX;
            playerY += speedY;
            playerZ += speedZ;

            // ensure the float values are converted to strings using the invariant culture
            var playerXString = playerX.ToString(CultureInfo.InvariantCulture);
            var playerYString = playerY.ToString(CultureInfo.InvariantCulture);
            var playerZString = playerZ.ToString(CultureInfo.InvariantCulture);

            if (valueManipulated)
            {
                // write the memory
                m.WriteMemory(xAddrString, "float", playerXString);
                m.WriteMemory(yAddrString, "float", playerYString);
                m.WriteMemory(zAddrString, "float", playerZString);
            }
        }

        private void teleport()
        {
            var isArrived = false;

            #region autoCancelTP

            if (!attachToCamEnabled)
            {
                if (lastPos.X == xCoord && lastPos.Y == yCoord && lastPos.Z == zCoord) rubberbandCount++;

                if (rubberbandCount > 3)
                {
                    doglide();
                    nofallFunction();
                    tpflag = false;
                    return;
                }
            }

            lastPos.X = xCoord;
            lastPos.Y = yCoord;
            lastPos.Z = zCoord;

            #endregion

            if (!glideEnabled)
            {
                doglide(); //to enable glide while teleport
                m.WriteMemory(movementModeAddrStr, "int", "6"); //to enable noclip while teleport
            }

            if (savedX == 0 || savedY == 0 || savedZ == 0)
            {
                log_console.Text += "\n\r\n\rInvalid Value!";
                return;
            }

            var distance = (float)Math.Sqrt(Math.Pow(xCoord - savedX, 2) + Math.Pow(yCoord - savedY, 2) +
                                            Math.Pow(zCoord - savedZ, 2));

            if (distance <= 1.0f && distance > 0)
            {
                // if player is already close to the destination tp directly
                m.WriteMemory(xAddrString, "float", savedX.ToString(CultureInfo.InvariantCulture));
                m.WriteMemory(yAddrString, "float", savedY.ToString(CultureInfo.InvariantCulture));
                m.WriteMemory(zAddrString, "float", savedZ.ToString(CultureInfo.InvariantCulture));
                Thread.Sleep(50);
                // m.WriteMemory(yAddrString, "float", (floorYValue).ToString(CultureInfo.InvariantCulture)); //bad idea (freecamtp)
                m.WriteMemory(movementModeAddrStr, "int", "1");

                isArrived = true;
                doglide();
            }
            else if (distance > 1.0f)
            {
                // calculate the normalized movement increments
                var moveX = (savedX - xCoord) / distance;
                var moveY = (savedY - yCoord) / distance;
                var moveZ = (savedZ - zCoord) / distance;

                // move towards the saved coordinates using normalized values
                m.WriteMemory(xAddrString, "float", (xCoord + moveX).ToString(CultureInfo.InvariantCulture));
                m.WriteMemory(yAddrString, "float", (yCoord + moveY).ToString(CultureInfo.InvariantCulture));
                m.WriteMemory(zAddrString, "float", (zCoord + moveZ).ToString(CultureInfo.InvariantCulture));

                Thread.Sleep(100);

                // check if the player has arrived
                isArrived = distance <= 1.0f;
            }

            if (isArrived)
            {
                isArrived = false;
                tpflag = false;
            }
        }

        private void teleportNumpad()
        {
            while (true)
            {
                var playerXCoord = m.ReadFloat(xAddrString);
                var playerYCoord = m.ReadFloat(yAddrString);
                var playerZCoord = m.ReadFloat(zAddrString);
                var isTPUpPressed = sim.InputDeviceState.IsHardwareKeyDown(TPUpKey);
                var isTPDownPressed = sim.InputDeviceState.IsHardwareKeyDown(TPDownKey);
                var isTPXUpPressed = sim.InputDeviceState.IsHardwareKeyDown(TPLeftKey);
                var isTPXDownPressed = sim.InputDeviceState.IsHardwareKeyDown(TPRightKey);
                var isTPZUpPressed = sim.InputDeviceState.IsHardwareKeyDown(TPForwardKey);
                var isTPZDownPressed = sim.InputDeviceState.IsHardwareKeyDown(TPBackwardKey);

                // numpad teleport
                if (isTPUpPressed)
                    m.WriteMemory(yAddrString, "float", (playerYCoord + 0.5f).ToString(CultureInfo.InvariantCulture));

                if (isTPDownPressed) m.WriteMemory(yAddrString, "float", (playerYCoord - 0.25f).ToString());

                if (isTPXUpPressed)
                    m.WriteMemory(xAddrString, "float", (playerXCoord + 0.25f).ToString(CultureInfo.InvariantCulture));

                if (isTPXDownPressed)
                    m.WriteMemory(xAddrString, "float", (playerXCoord - 0.25f).ToString(CultureInfo.InvariantCulture));

                if (isTPZUpPressed)
                    m.WriteMemory(zAddrString, "float", (playerZCoord + 0.25f).ToString(CultureInfo.InvariantCulture));

                if (isTPZDownPressed)
                    m.WriteMemory(zAddrString, "float", (playerZCoord - 0.25f).ToString(CultureInfo.InvariantCulture));

                Thread.Sleep(100);
            }
        }

        private string convertUintToHexString(UIntPtr uintToConvert)
        {
            string placeholder1 = ((ulong)uintToConvert).ToString(CultureInfo.InvariantCulture);
            var placeholder2 = ulong.Parse(placeholder1);
            var hexstring = placeholder2.ToString("X2",CultureInfo.InvariantCulture);
            return hexstring;
        }

        private static UIntPtr ParseHexToUIntPtr(string hexValue)
        {
            // Parse the hexadecimal string into a UIntPtr
            var numericValue = ulong.Parse(hexValue, NumberStyles.HexNumber);

            return new UIntPtr(numericValue);
        }

        private void nofallFunction()
        {
            if (!nofallEnabled)
            {
                if (!nofallPatched)
                {
                    m.CreateCodeCave(nofallAddrString, patchedBytes, 6, 120);

                    gotoCaveBytes = m.ReadBytes(nofallAddrString, 6);

                    nofallEnabled = true;
                    nofallPatched = true;
                }
                else
                {
                    m.WriteBytes(nofallAddr, gotoCaveBytes);
                    nofallEnabled = true;
                }
            }
            else
            {
                nofallAddr = ParseHexToUIntPtr(nofallAddrString);
                m.WriteBytes(nofallAddr, originalBytes);
                nofallEnabled = false;
            }
        }

        private void infReachFunction()
        {
            if (!infReachEnabled)
            {
                if (!infReachPatched)
                {
                    m.CreateCodeCave(infReachAddressStr, infReachPatchedBytes, 6, 300);

                    infReachAlreadyPatchedBytes = m.ReadBytes(infReachAddressStr, 6);

                    infReachEnabled = true;
                    infReachPatched = true;
                }
                else
                {
                    m.WriteBytes(infReachAddressStr, infReachAlreadyPatchedBytes);
                    infReachEnabled = true;
                }
            }
            else
            {
                m.WriteBytes(infReachAddressStr, infReachOriginalBytes);
                infReachEnabled = false;
            }
        }

        private bool onlineCheck(string url)
        {
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    log_console.Text += "Tool is Online";
                }
                else
                {
                    char[] msg = { 'T', 'o', 'o', 'l', ' ', 'O', 'f', 'f', 'l', 'i', 'n', 'e' };
                    var prnt = new StringBuilder();

                    foreach (var c in msg)
                        prnt.Append(c);


                    MessageBox.Show(prnt.ToString());
                    Environment.Exit(0);
                }

                return response.IsSuccessStatusCode;
            }
        }

        private void updateCheck(string url)
        {
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(url).Result;

                if (!response.IsSuccessStatusCode)
                {
                    var openDc =
                        MessageBox.Show(
                            "Your tool is outdated. Please download a new version from the discord.\r\nWould you like to join the discord?",
                            "Error", MessageBoxButtons.YesNo);

                    if (openDc == DialogResult.Yes) openDiscord();

                    Environment.Exit(1);
                }
                else
                {
                    log_console.Text += "\r\n\r\nYou are running the latest version\r\n\r\n";
                }
            }
        }

        // private void PreventProgramFromBeingDebuged()
        // {
        //    var a = DateTime.Now;
        //    Thread.Sleep(10);
        //    var b = DateTime.Now;
        //    if ((b - a).TotalSeconds > 2)
        //    {
        //        Application.ExitThread();
        //        Environment.Exit(0);
        //        this.Close();
        //    }
        // }
        private void startMainTimer()
        {
            try
            {
                mainTimer.Start();
            }
            catch (Exception ex)
            {
                log_console.Text += $"\r\nException in Main Thread: {ex.Message}";
            }
        }

        private void startPvPTimer()
        {
            try
            {
                pvpTimer.Start();
            }
            catch (Exception ex)
            {
                log_console.Text += $"\r\nException in PVP Thread: {ex.Message}";
            }
        }

        private void startgetBaseTimer()
        {
            try
            {
                timer_getBase.Start();
            }
            catch (Exception ex)
            {
                log_console.Text += $"\r\nException in getBase Thread: {ex.Message}";
            }
        }

        private void createCodeCave()
        {
            if (!noclipCave)
            {
                try
                {
                    byte[] patched_bytes =
                    {
                       0x83, 0xB9, 0xF4, 0x02, 0x00, 0x00, 0x02, 0x0F, 0x85, 0x07, 0x00, 0x00, 0x00, 0x48, 
                       0x89, 0x0D, 0x0C, 0x00, 0x00, 0x00, 0x48, 0x8B, 0x01, 0x48, 0x8B, 0x40, 0x58
                    };

                    // Create Codecave
                    pbasecaveAddr = m.CreateCodeCave(noclipAddressStr, patched_bytes, 7, 120);

                    log_console.Invoke((MethodInvoker)delegate
                    {
                        log_console.Text += "\r\n\r\nHooked";
                    });

                    // rest of code
                    noclipPatchedBytes = m.ReadBytes(noclipAddressStr, 7);
                    noclipPatched = true;
                    noclipCave = true;
                    // log_console.Text = log_console.Text + "\r\n\r\nCave Created";;
                }
                catch (Exception ex)
                {
                    log_console.Invoke((MethodInvoker)delegate
                    {
                        log_console.Text += $"\r\n\r\nHook failed\r\n\r\nPlease restart the game\r\n\r\nError: {ex.Message}";
                    });

                    return;
                }
            }
            else
            {
                if (noclipPatched)
                {
                    m.WriteBytes(noclipAddressStr, noclipBytes);

                    log_console.Invoke((MethodInvoker)delegate
                    {
                        log_console.Text += "\r\n\r\nUnhooked";
                    });

                    noclipPatched = false;
                }
                else
                {
                    m.WriteBytes(noclipAddressStr, noclipPatchedBytes);

                    log_console.Invoke((MethodInvoker)delegate
                    {
                        log_console.Text += "\r\n\r\nRe-Hooked";
                    });

                    noclipPatched = true;
                }
            }

            // camera

            if (!camCave)
            {
                byte[] patched_bytes =
                {
                    0x81, 0x7F, 0x4C, 0xF4, 0x7D, 0x00, 0x00, 0x0F, 0x85, 0x07, 0x00, 0x00, 0x00, 0x48, 0x89, 
                    0x3D, 0x0D, 0x00, 0x00, 0x00, 0xF3, 0x0F, 0x10, 0x87, 0xD0, 0x03, 0x00, 0x00
                }; // patched bytes from the asm above

                // Create Codecave
                var caveAddr = m.CreateCodeCave(cameraAddress, patched_bytes, 8, 220); //crash

                // logCaveAddr
                var caveAddrString = convertUintToHexString(caveAddr);
                // log_console.Text = log_console.Text + "\r\n\r\nCaveAddr = " + caveAddrString;

                // Add offset 0x12 to the cave addr (that's where the ptr for pbase is stored)
                camBaseUInt = UIntPtr.Add(caveAddr, 0x21); //caveAddr == UIntPtr
                // log caveAddr + offset
                var CambaseUIntString = convertUintToHexString(camBaseUInt);
                // log_console.Text = log_console.Text + "\r\n\r\nAddress of the Cam PTR = " + CambaseUIntString;

                Thread.Sleep(100);

                // readValue & convert to hex string
                var camBaselong = m.ReadLong(CambaseUIntString);
                CamBaseAddress = camBaselong.ToString("X2", CultureInfo.InvariantCulture);
                camBaseUInt = ParseHexToUIntPtr(CamBaseAddress);

                // log to console
                // log_console.Text = log_console.Text + "\r\n\r\nCamBaseAddress = " + CamBaseAddress;

                // rest of code
                cameraPatchedBytes = m.ReadBytes(cameraAddress, 8);
                cameraPatched = true;
                camCave = true;
                // log_console.Text = log_console.Text + "\r\n\r\nCave Created";
            }
            else
            {
                if (cameraPatched)
                {
                    m.WriteBytes(cameraAddress, cameraBytes);
                    // log_console.Text = log_console.Text + "\r\n\r\nBytes Restored";
                    cameraPatched = false;
                }
                else
                {
                    m.WriteBytes(cameraAddress, cameraPatchedBytes);
                    // log_console.Text = log_console.Text + "\r\n\r\nBytes Patched";
                    cameraPatched = true;
                }
            }

            if (!speedHackCave)
            {
                byte[] patched_bytes =
                {
                    0x83, 0x3D, 0x23, 0x00, 0x00, 0x00, 0x00, 0x0F, 0x84, 0x10, 0x00, 0x00, 0x00, 0x53, 0x48, 0x8B,
                    0x1D, 0x15, 0x00, 0x00, 0x00, 0x48, 0x89, 0x9E, 0xDC, 0x00, 0x00, 0x00, 0x5B, 0xF3, 0x0F, 0x10,
                    0xBE, 0xDC, 0x00, 0x00, 0x00
                }; // patched bytes from the asm above

                // Create Codecave
                var caveAddr = m.CreateCodeCave(speedHackAddrString, patched_bytes, 8, 120);

                // logCaveAddr
                var caveAddrString = convertUintToHexString(caveAddr);
                // log_console.Text = log_console.Text + "\r\n\r\nCaveAddr = " + caveAddrString;

                // Add offset 0x12 to the cave addr (that's where the ptr for pbase is stored)
                speedValueUInt = UIntPtr.Add(caveAddr, 0x2A); //caveAddr == UIntPtr
                // log caveAddr + offset
                speedValueUIntString = convertUintToHexString(speedValueUInt);
                // log_console.Text = log_console.Text + "\r\n\r\nAddress of the Speed PTR = " + speedValueUIntString;

                Thread.Sleep(100);

                // rest of code
                speedPatchedBytes = m.ReadBytes(speedHackAddrString, 8);
                speedPatched = true;
                speedHackCave = true;
                // log_console.Text = log_console.Text + "\r\n\r\nCave Created";
            }
            else
            {
                if (speedPatched)
                {
                    m.WriteBytes(speedHackAddrString, speedBytes);
                    // log_console.Text = log_console.Text + "\r\n\r\nBytes Restored";
                    speedPatched = false;
                }
                else
                {
                    m.WriteBytes(speedHackAddrString, speedPatchedBytes);
                    // log_console.Text = log_console.Text + "\r\n\r\nBytes Patched";
                    speedPatched = true;
                }
            }

            Cursor.Current = Cursors.Default;
        }

        private void logToConsole(string textToLog)
        {
            log_console.Text += $"\r\n{textToLog}";
        }

        /*
        private void checkForPvP()
        {
            ////Takes up to much performance

            //while (!isPVPEnabled)
            //{
            //    Thread.Sleep(10000);

            //    string aobScanResult = m.AoBScan(PVPAOB, true, false).Result.Sum().ToString("X2");

            //    if (aobScanResult == "00")
            //    {
            //        isPVPEnabled = true;
            //        MessageBox.Show("No PvP allowed ;)");
            //        Environment.Exit(0);
            //    }
            //}
        }
        */
        private void doglide()
        {
            if (!glideEnabled)
            {
                glideEnabled = true;

                m.WriteMemory(glideAddrString, "bytes", "90 90 90 90 90 90");
                m.WriteMemory(movementModeAddrStr, "int", "6");
            }
            else
            {
                glideEnabled = false;

                m.WriteMemory(glideAddrString, "bytes", glideAOB);
                m.WriteMemory(movementModeAddrStr, "int", "1");
            }
        }

        private void speedhackFunction()
        {
            if (!isSpeedhackEnabled)
            {
                box_speedhack.Invoke((MethodInvoker)delegate
                {
                    box_speedhack.Text = "On";
                    box_speedhack.Checked = true;
                });

                isSpeedhackEnabled = true;
            }
            else
            {
                box_speedhack.Invoke((MethodInvoker)delegate
                {
                    box_speedhack.Text = "Off";
                    box_speedhack.Checked = false;
                });

                isSpeedhackEnabled = false;
            }
        }

        private void tpToCam()
        {
            rubberbandCount = 0;

            savedX = m.ReadFloat(xCamAddrString);
            savedY = m.ReadFloat(yCamAddrString);
            savedZ = m.ReadFloat(zCamAddrString);

            log_console.Text += "\r\n\r\nTeleported to camera";

            // BottomScroll
            log_console.Focus();
            log_console.ScrollToCaret();
            log_console.SelectionLength = 0;
            // BottomScroll

            saveflag = true;

            tpflag = true;
            box_nofall.Checked = true;
        }

        private void hotkeysFunction()
        {
            while (true)
            {
                var isinfJumpKeyPressed = sim.InputDeviceState.IsHardwareKeyDown(infJumpKey);
                var isinfJumpKeyUP = sim.InputDeviceState.IsHardwareKeyUp(infJumpKey);
                var isFreecamKeyPressed = sim.InputDeviceState.IsHardwareKeyDown(FreecamKey);
                var isTPToCamKeyPressed = sim.InputDeviceState.IsHardwareKeyDown(TPToCamKey);
                var isNofallKeyPressed = sim.InputDeviceState.IsHardwareKeyDown(NofallKey);
                var isGlideKeyPressed = sim.InputDeviceState.IsHardwareKeyDown(GlideKey);
                var isSpeedKeyPressed = sim.InputDeviceState.IsHardwareKeyDown(SpeedKey);
                var spamKeyPressed = sim.InputDeviceState.IsKeyDown(enableSpamKey);

                try
                {
                    if (spamKeyPressed)
                    {
                        enableSpam = !enableSpam;
                        Thread.Sleep(200);
                    }

                    if (isInfJumpActivated)
                    {
                        if (isinfJumpKeyPressed && !isInfJumpToggled)
                        {
                            Invoke((MethodInvoker)delegate
                            {
                                // box_infJump.Checked = !box_infJump.Checked;
                                infJumpPatch();
                            });

                            isInfJumpToggled = true;
                            Thread.Sleep(200);
                        }

                        if (isinfJumpKeyUP && isInfJumpToggled)
                        {
                            Invoke((MethodInvoker)delegate
                            {
                                // box_infJump.Checked = !box_infJump.Checked;
                                infJumpPatch();
                            });

                            isInfJumpToggled = false;
                            Thread.Sleep(200);
                        }
                    }

                    if (isFreecamKeyPressed)
                    {
                        Invoke((MethodInvoker)delegate { toggle_freecam(); });

                        Thread.Sleep(200);
                    }

                    if (isTPToCamKeyPressed)
                    {
                        btn_tpToCam.PerformClick();
                        Thread.Sleep(200);
                    }

                    if (isNofallKeyPressed)
                    {
                        nofallFunction();
                        Thread.Sleep(200);
                    }

                    if (isGlideKeyPressed)
                    {
                        doglide();
                        Thread.Sleep(200);
                    }

                    if (isSpeedKeyPressed)
                    {
                        speedhackFunction();
                        Thread.Sleep(200);
                    }
                }
                catch
                {
                }
            }
        }

        private void openDiscord()
        {
            // Open discord
            var tempFile = Path.GetTempFileName() + ".bat";

            // Command to run Python script
            var blockCmd = @"start https://discord.gg/AkdunrkRBD";

            // Write the commands to the temporary batch file
            File.WriteAllText(tempFile, blockCmd);

            // Configure the process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe", // Specify cmd.exe as the program to run
                Arguments = "/c \"" + tempFile + "\"", // Pass the batch file as an argument to cmd.exe
                WindowStyle = ProcessWindowStyle.Hidden, // Hide the window
                CreateNoWindow = true, // Do not create a window
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false
            };

            // Start the process
            var process = Process.Start(startInfo);

            // Wait for the process to exit asynchronously
            Task.Run(() => process.WaitForExit());
        }

        // private void saveLocations()
        // {
        //    using (FileStream fs = new FileStream("locations.dat", FileMode.Create))
        //    {
        //        BinaryFormatter formatter = new BinaryFormatter();
        //        formatter.Serialize(fs, locationList);
        //    }
        //    MessageBox.Show("Saved locations");
        // }
        private void saveLocations()
        {
            try
            {
                using (var writer = new StreamWriter("locations.xml"))
                {
                    var serializer = new XmlSerializer(typeof(List<customLocation>));
                    serializer.Serialize(writer, locationList);
                }

                MessageBox.Show("Saved locations");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving locations: {ex.Message}");
            }
        }

        private void loadLocations()
        {
            try
            {
                if (File.Exists("locations.xml"))
                    using (var reader = new StreamReader("locations.xml"))
                    {
                        var serializer = new XmlSerializer(typeof(List<customLocation>));
                        locationList = (List<customLocation>)serializer.Deserialize(reader);

                        // Sort the list by customName
                        locationList = locationList.OrderBy(loc => loc.customName).ToList();

                        // Update the listbox
                        listbox_teleportLocations.Items.Clear();

                        foreach (var loc in locationList)
                            listbox_teleportLocations.Items.Add(loc.customName);
                    }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading locations: {ex.Message}");
            }
        }

        private void restoreOriginalCode()
        {
            try
            {
                m.WriteBytes(noclipAddressStr, noclipBytes);
                noclipPatched = false;

                m.WriteBytes(cameraAddress, cameraBytes); //crash
                cameraPatched = false;

                m.WriteBytes(speedHackAddrString, speedBytes);
                speedPatched = false;

                m.WriteMemory(nofallAddrString, "bytes", nofallAOB);
                nofallEnabled = false;

                m.WriteMemory(glideAddrString, "bytes", glideAOB);
                glideEnabled = false;

                m.WriteMemory(camCollisionAddrStr, "bytes", "F3 0F 11 8F 50 03 00 00");
                camCollisionEnabled = false;

                m.WriteMemory(noAnimationAddrString, "bytes", "F3 0F 11 8B 70 02 00 00");
                noAnimationPatched = false;

                m.WriteMemory(infJumpAddrStr, "bytes", "F2 0F 11 47 0C");
                infJumpPatched = false;

                m.WriteBytes(cameraYUInt, cameraYBytes);
                cameraYPatched = false;

                m.WriteBytes(cameraZUInt, cameraZBytes);
                cameraZPatched = false;

                m.WriteMemory($"{wallhackAddress}", "bytes", "74 0D");
                wallhackPatched = false;

                m.WriteMemory($"{wallhack2Address}", "bytes", "0F 84");
                wallhack2Patched = false;

                m.WriteMemory(movementModeAddrStr, "int", "1");

                m.WriteBytes(infReachAddressStr, infReachOriginalBytes);
            }
            catch
            {
                MessageBox.Show("Code restoration failed. You might want to restart your game.");
            }
        }

        private void antiAFKfunc()
        {
            var timePassed = DateTime.Now - dateTimeBuffer; // Calculate the time difference

            if (timePassed.TotalMinutes > 5)
            {
                sim.Keyboard.KeyDown(VirtualKeyCode.VK_W);
                Thread.Sleep(100);
                sim.Keyboard.KeyUp(VirtualKeyCode.VK_W);

                dateTimeBuffer = DateTime.Now;
            }
        }

        #endregion

        #region Trackbars

        private void trck_wFloor_Scroll(object sender, EventArgs e)
        {
            switch (trck_wFloor.Value)
            {
                case 0:
                    m.WriteMemory(wFloorAddrStr, "float", "0.01999999955");
                    break;

                default:
                    m.WriteMemory(wFloorAddrStr, "float", $"{(float)trck_wFloor.Value / 2}");
                    break;
            }
        }

        private void trckbar_camSpeed_Scroll(object sender, EventArgs e)
        {
            camSpeed = float.Parse(trckbar_camSpeed.Value.ToString(CultureInfo.InvariantCulture)) / 25f;
        }

        private void trckbr_speed_Scroll(object sender, EventArgs e)
        {
            if (isSpeedhackEnabled)
            {
                m.WriteMemory(speedValueUIntString, "float", trckbr_speed.Value.ToString(CultureInfo.InvariantCulture));
                // BottomScroll
                log_console.Focus();
                log_console.ScrollToCaret();
                log_console.SelectionLength = 0;
                // BottomScroll
            }
        }

        private void trck_opcacity_Scroll(object sender, EventArgs e)
        {
            var newOpacity = (float)trck_opcacity.Value / 100;
            Opacity = newOpacity;
        }

        #endregion

        #region Buttons

        private void btn_clearConsole_Click(object sender, EventArgs e)
        {
            log_console.Text = "";
        }

        private void btn_saveLocation_Click(object sender, EventArgs e)
        {
            savedX = xCoord;
            savedY = yCoord;
            savedZ = zCoord;

            txt_XBox.Text = xCoord.ToString(CultureInfo.InvariantCulture);
            txt_YBox.Text = yCoord.ToString(CultureInfo.InvariantCulture);
            txt_ZBox.Text = zCoord.ToString(CultureInfo.InvariantCulture);

            log_console.Text += $"\r\n\r\nPosition saved at\r\nX: {savedX}\r\nY: {savedY}\r\nZ: {savedZ}\r\n";
            // BottomScroll
            log_console.Focus();
            log_console.ScrollToCaret();
            log_console.SelectionLength = 0;
            // BottomScroll

            saveflag = true;
        }

        private void btn_tpToCam_Click(object sender, EventArgs e)
        {
            tpToCam();
        }

        private void btn_teleport_Click(object sender, EventArgs e)
        {
            tpflag = true;
            box_nofall.Checked = true;
            rubberbandCount = 0;
            // BottomScroll
            log_console.Focus();
            log_console.ScrollToCaret();
            log_console.SelectionLength = 0;
            // BottomScroll
        }

        private void btn_saveCustomCoords_Click(object sender, EventArgs e)
        {
            try
            {
                if (txt_XBox.Text.Contains(",") || txt_YBox.Text.Contains(",") || txt_ZBox.Text.Contains(","))
                {
                    txt_XBox.Text.Replace(",", ".");
                    txt_YBox.Text.Replace(",", ".");
                    txt_ZBox.Text.Replace(",", ".");
                }

                savedX = float.Parse(txt_XBox.Text);
                savedY = float.Parse(txt_YBox.Text);
                savedZ = float.Parse(txt_ZBox.Text);

                log_console.Text += $"\r\n\r\nPosition saved at\r\nX: {savedX}\r\nY: {savedY}\r\nZ: {savedZ}\r\n";
            }
            catch
            {
                log_console.Text += "\r\n\r\nPosition failed saving!\r\nMaybe the wrong formatting? (123.34)";
            }

            // BottomScroll
            log_console.Focus();
            log_console.ScrollToCaret();
            log_console.SelectionLength = 0;
            // BottomScroll
        }

        private void btn_cancelTP_Click(object sender, EventArgs e)
        {
            tpflag = false;
            nofallFunction();
        }

        private void btn_addCustomTP_Click(object sender, EventArgs e)
        {
            // Ensure locationList is initialized as a List<customLocation>
            if (locationList == null) locationList = new List<customLocation>();

            var newLocation = new customLocation
            {
                customName = txt_tpnameBox.Text
            };

            // Assuming customX, customY, customZ are of type float, convert the text appropriately
            if (float.TryParse(txt_XBox.Text, out var xValue))
            {
                newLocation.customX = xValue;
            }
            else
            {
                // Handle the case where the conversion fails
                MessageBox.Show("Invalid X value");
                return;
            }

            if (float.TryParse(txt_YBox.Text, out var yValue))
            {
                newLocation.customY = yValue;
            }
            else
            {
                // Handle the case where the conversion fails
                MessageBox.Show("Invalid Y value");
                return;
            }

            if (float.TryParse(txt_ZBox.Text, out var zValue))
            {
                newLocation.customZ = zValue;
            }
            else
            {
                // Handle the case where the conversion fails
                MessageBox.Show("Invalid Z value");
                return;
            }

            // Add the new location to the list
            locationList.Add(newLocation);

            // Sort the list by customName
            locationList = locationList.OrderBy(loc => loc.customName).ToList();

            // Update the listbox with the sorted list
            listbox_teleportLocations.Items.Clear(); // Clear existing items to avoid duplicates

            foreach (var loc in locationList)
                listbox_teleportLocations.Items.Add(loc.customName);
        }

        private void btn_setSelected_Click(object sender, EventArgs e)
        {
            savedX = locationList[listbox_teleportLocations.SelectedIndex].customX;
            savedY = locationList[listbox_teleportLocations.SelectedIndex].customY;
            savedZ = locationList[listbox_teleportLocations.SelectedIndex].customZ;
        }

        private void btn_clearHotkeys_Click(object sender, EventArgs e)
        {
            // Clear the text in each textbox
            txtbox_TPUpKey.Text = "";
            txtbox_TPDowNkey.Text = "";
            txtbox_TPLeftKey.Text = "";
            txtbox_TPRightKey.Text = "";
            txtbox_TPForwardKey.Text = "";
            txtbox_TPBackwardKey.Text = "";
            txtbox_freecamKey.Text = "";
            txtbox_tpToCamKey.Text = "";
            txtbox_nofallKey.Text = "";
            txtbox_glideKey.Text = "";
            txtbox_speedKey.Text = "";
            txtbox_flybackwardskey.Text = "";
            txtbox_flyforwardskey.Text = "";
            txtbox_flyUp.Text = "";
            txtbox_flyDown.Text = "";
            txt_hotkeySpam.Text = "";

            // Reset the associated hotkey variables to a default value
            infJumpKey = VirtualKeyCode.NONAME;
            TPUpKey = VirtualKeyCode.NONAME;
            TPDownKey = VirtualKeyCode.NONAME;
            TPLeftKey = VirtualKeyCode.NONAME;
            TPRightKey = VirtualKeyCode.NONAME;
            TPForwardKey = VirtualKeyCode.NONAME;
            TPBackwardKey = VirtualKeyCode.NONAME;
            FreecamKey = VirtualKeyCode.NONAME;
            TPToCamKey = VirtualKeyCode.NONAME;
            NofallKey = VirtualKeyCode.NONAME;
            GlideKey = VirtualKeyCode.NONAME;
            SpeedKey = VirtualKeyCode.NONAME;
            forwardsKey = VirtualKeyCode.NONAME;
            backwardsKey = VirtualKeyCode.NONAME;
            flyDownKey = VirtualKeyCode.NONAME;
            flyUpKey = VirtualKeyCode.NONAME;
            enableSpamKey = VirtualKeyCode.NONAME;
        }

        private void btn_saveHotkeys_Click(object sender, EventArgs e)
        {
            var settings = new HotkeySettings
            {
                TPUpKey = TPUpKey,
                TPDownKey = TPDownKey,
                TPLeftKey = TPLeftKey,
                TPRightKey = TPRightKey,
                TPForwardKey = TPForwardKey,
                TPBackwardKey = TPBackwardKey,
                FreecamKey = FreecamKey,
                TPToCamKey = TPToCamKey,
                NofallKey = NofallKey,
                GlideKey = GlideKey,
                SpeedKey = SpeedKey,
                flyforwardsKey = forwardsKey,
                flybackwardsKey = backwardsKey,
                flyDownKey = flyDownKey,
                flyupKey = flyUpKey,
                enableSpamKey = enableSpamKey
            };

            using (var fs = new FileStream("hotkeys.dat", FileMode.Create))
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(fs, settings);
            }

            MessageBox.Show("Hotkeys Saved");
        }

        private void btn_saveLocations_Click(object sender, EventArgs e)
        {
            try
            {
                saveLocations();
            }
            catch
            {
            }
        }

        private void btn_clearLocations_Click(object sender, EventArgs e)
        {
            locationList = null;
            listbox_teleportLocations.Items.Clear();
        }

        private void btn_loadLocations_Click(object sender, EventArgs e)
        {
            loadLocations();
        }

        private void btn_removeLocation_Click(object sender, EventArgs e)
        {
            locationList.RemoveAt(listbox_teleportLocations.SelectedIndex);
            listbox_teleportLocations.Items.RemoveAt(listbox_teleportLocations.SelectedIndex);
        }

        #endregion

        #region AssignedHotkeys

        private void txtbox_TpUpHotkey_KeyDown(object sender, KeyEventArgs e)
        {
            // TPUpKey == VirtualKeyCode
            TPUpKey = (VirtualKeyCode)e.KeyCode;
            txtbox_TPUpKey.Text = TPUpKey.ToString();
        }

        private void txtbox_TPDowNkey_KeyDown(object sender, KeyEventArgs e)
        {
            TPDownKey = (VirtualKeyCode)e.KeyCode;
            txtbox_TPDowNkey.Text = TPDownKey.ToString();
        }

        private void txtbox_TPLeftKey_KeyDown(object sender, KeyEventArgs e)
        {
            TPLeftKey = (VirtualKeyCode)e.KeyCode;
            txtbox_TPLeftKey.Text = TPLeftKey.ToString();
        }

        private void txtbox_TPRightKey_KeyDown(object sender, KeyEventArgs e)
        {
            TPRightKey = (VirtualKeyCode)e.KeyCode;
            txtbox_TPRightKey.Text = TPRightKey.ToString();
        }

        private void txtbox_TPForwardKey_KeyDown(object sender, KeyEventArgs e)
        {
            TPForwardKey = (VirtualKeyCode)e.KeyCode;
            txtbox_TPForwardKey.Text = TPForwardKey.ToString();
        }

        private void txtbox_TPBackwardKey_KeyDown(object sender, KeyEventArgs e)
        {
            TPBackwardKey = (VirtualKeyCode)e.KeyCode;
            txtbox_TPBackwardKey.Text = TPBackwardKey.ToString();
        }

        private void txtbox_freecamKey_KeyDown(object sender, KeyEventArgs e)
        {
            FreecamKey = (VirtualKeyCode)e.KeyCode;
            txtbox_freecamKey.Text = FreecamKey.ToString();
        }

        private void txtbox_tpToCamKey_KeyDown(object sender, KeyEventArgs e)
        {
            TPToCamKey = (VirtualKeyCode)e.KeyCode;
            txtbox_tpToCamKey.Text = TPToCamKey.ToString();
        }

        private void txtbox_nofallKey_KeyDown(object sender, KeyEventArgs e)
        {
            NofallKey = (VirtualKeyCode)e.KeyCode;
            txtbox_nofallKey.Text = NofallKey.ToString();
        }

        private void txtbox_glideKey_KeyDown(object sender, KeyEventArgs e)
        {
            GlideKey = (VirtualKeyCode)e.KeyCode;
            txtbox_glideKey.Text = GlideKey.ToString();
        }

        private void txtbox_speedKey_KeyDown(object sender, KeyEventArgs e)
        {
            SpeedKey = (VirtualKeyCode)e.KeyCode;
            txtbox_speedKey.Text = SpeedKey.ToString();
        }

        private void txtbox_flyforwardskey_KeyDown(object sender, KeyEventArgs e)
        {
            forwardsKey = (VirtualKeyCode)e.KeyCode;
            txtbox_flyforwardskey.Text = forwardsKey.ToString();
        }

        private void txtbox_flybackwardskey_KeyDown(object sender, KeyEventArgs e)
        {
            backwardsKey = (VirtualKeyCode)e.KeyCode;
            txtbox_flybackwardskey.Text = backwardsKey.ToString();
        }

        private void txtbox_flyDown_KeyDown(object sender, KeyEventArgs e)
        {
            flyDownKey = (VirtualKeyCode)e.KeyCode;
            txtbox_flyDown.Text = flyDownKey.ToString();
        }

        private void txtbox_flyUp_KeyDown(object sender, KeyEventArgs e)
        {
            flyUpKey = (VirtualKeyCode)e.KeyCode;
            txtbox_flyUp.Text = flyUpKey.ToString();
        }

        private void txt_hotkeySpam_KeyDown(object sender, KeyEventArgs e)
        {
            enableSpamKey = (VirtualKeyCode)e.KeyCode;
            txt_hotkeySpam.Text = enableSpamKey.ToString();
        }

        #endregion

        #region structs

        [Serializable]
        public struct customLocation
        {
            public string customName;
            public float customX;
            public float customY;
            public float customZ;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            openDiscord();
        }

        private void listbox_teleportLocations_DoubleClick(object sender, EventArgs e)
        {
            savedX = locationList[listbox_teleportLocations.SelectedIndex].customX;
            savedY = locationList[listbox_teleportLocations.SelectedIndex].customY;
            savedZ = locationList[listbox_teleportLocations.SelectedIndex].customZ;
        }

        #endregion

        #region misc

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            creditClickerCount++;
            lbl_credits4.Text = $"Count : {creditClickerCount}";
            pnl_creditClicker.BackColor = Color.FromArgb(rnd.Next(255), rnd.Next(255), rnd.Next(255));
        }

        private void materialTabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Text = materialTabControl1.SelectedTab.Text;
        }

        #endregion
    }
}