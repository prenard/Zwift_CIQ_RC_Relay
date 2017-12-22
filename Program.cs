/*
 * Created by SharpDevelop.
 * User: admin
 * Date: 18/12/2017
 * Time: 09:56
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using ANT_Managed_Library;
using AntPlus.Profiles.Controls;
using AntPlus.Profiles.Components;
using AntPlus.Types;

using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using WindowsInput;

namespace Zwift_CIQ_RC_Relay
{
	class Program
	{
		[DllImport ("User32.dll")]
		static extern int SetForegroundWindow(IntPtr point);

		[DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
		
		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int sizeOfInputStructure);

		// Structures for sendinput
		
		[StructLayout( LayoutKind.Explicit )]
	    public struct INPUT
		{
	        [FieldOffset( 0 )]
	        public int type;
	        [FieldOffset( 4 )]
	        public MOUSEINPUT mi;
	        [FieldOffset( 4 )]
	        public KEYBDINPUT ki;
	        [FieldOffset( 4 )]
	        public HARDWAREINPUT hi;
	    }
 
	    [StructLayout( LayoutKind.Sequential )]
    	public struct MOUSEINPUT
    	{
    	    public int dx;
    	    public int dy;
    	    public uint mouseData;
    	    public uint dwFlags;
    	    public uint time;
    	    public IntPtr dwExtraInfo;
    	}
 
    	[StructLayout( LayoutKind.Sequential )]
    	public struct KEYBDINPUT
    	{
    	    public ushort wVk;
    	    public ushort wScan;
    	    public uint dwFlags;
    	    public uint time;
    	    public IntPtr dwExtraInfo;
    	}
 
    	[StructLayout( LayoutKind.Sequential )]
    	public struct HARDWAREINPUT
    	{
    	    public uint uMsg;
    	    public ushort wParamL;
    	    public ushort wParamH;
    	}

    	[Flags]
		private enum KeyEventF
		{
	    KeyDown = 0x0000,
	    ExtendedKey = 0x0001,
	    KeyUp = 0x0002,
	    Unicode = 0x0004,
	    ScanCode = 0x0008,
		}
    	
		// End of structutres for sendinput
		
		static string ProgramVersion = "Version 00.01";
	    static bool bDone;
		static readonly byte USER_ANT_CHANNEL = 0;         // ANT Channel to use
        static ushort USER_DEVICENUM = 0;       // Device number
        static readonly byte USER_DEVICETYPE = 16;         // Device type = 16 - Generic Remote Control
        static readonly byte USER_TRANSTYPE = 5;           // Transmission type
        static readonly byte USER_RADIOFREQ = 57;          // RF Frequency + 2400 MHz
		static readonly byte[] USER_NETWORK_KEY = { 0xB9, 0xA5, 0x21, 0xFB, 0xBD, 0x72, 0xC3, 0x45 };
        static readonly byte USER_NETWORK_NUM = 0;         // The network key is assigned to this network number

        static ANT_Device device0;
        static ANT_Channel channel0;
        static GenericControllableDevice genericControllableDevice;
        static Network networkAntPlus = new Network(USER_NETWORK_NUM, USER_NETWORK_KEY, USER_RADIOFREQ);

        public static void Main(string[] args)
		{
			Console.WriteLine("Zwift Garmin CIQ Remote Control Relay Program - " + ProgramVersion);

			try
            {
                Init();
                Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Program failed with exception: \n" + ex.Message);
            }

        }

		static void Init()
        {
            try
            {
                Console.WriteLine("Connecting to ANT USB device...");
                device0 = new ANT_Device();   // Create a device instance using the automatic constructor (automatic detection of USB device number and baud rate)
                device0.deviceResponse += new ANT_Device.dDeviceResponseHandler(DeviceResponse);    // Add device response function to receive protocol event messages
                channel0 = device0.getChannel(USER_ANT_CHANNEL);    // Get channel from ANT device
                channel0.channelResponse += new dChannelResponseHandler(ChannelResponse);  // Add channel response function to receive channel event messages
                Console.WriteLine("USB Dongle initialization successful");
                Console.WriteLine("USB Dongle Device Number: " + device0.getOpenedUSBDeviceNum());
                USER_DEVICENUM =  (ushort) device0.getOpenedUSBDeviceNum();
            }
            catch (Exception ex)
            {
                if (device0 == null)    // Unable to connect to ANT
                {
                    throw new Exception("Could not connect to any ANT device.\n" +
                    "Details: \n   " + ex.Message);
                }
                else
                {
                    throw new Exception("Error connecting to ANT device: " + ex.Message);
                }
            }
        }

        static void Start()
        {
            bDone = false;

            PrintMenu();

            try
            {
                ConfigureANT();
                while (!bDone)
                {
                    string command = Console.ReadLine();
                    switch (command)
                    {
                        case "M":
                        case "m":
                            {
                                PrintMenu();
                                break;
                            }
                        case "Q":
                        case "q":
                            {
                                // Quit
                                Console.WriteLine("Closing Channel");
                                channel0.closeChannel();
								System.Threading.Thread.Sleep(1000);
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                    System.Threading.Thread.Sleep(0);
                }
                // Clean up ANT
                Console.WriteLine("Disconnecting ANT Dongle...");
                ANT_Device.shutdownDeviceInstance(ref device0);  // Close down the device completely and completely shut down all communication
                Console.WriteLine("Application has completed successfully!");
                return;

            }
            catch (Exception ex)
            {
                throw new Exception("Error: " + ex.Message + Environment.NewLine);
            }
        }
        
        private static void ConfigureANT()
        {
            Console.WriteLine("Resetting ANT device...");
            device0.ResetSystem();     // Soft reset
            System.Threading.Thread.Sleep(500);    // Delay 500ms after a reset

            Console.WriteLine("Setting ANT network key...");
            if (device0.setNetworkKey(USER_NETWORK_NUM, USER_NETWORK_KEY, 500))
                Console.WriteLine("ANT network key setting successful");
            else
                throw new Exception("Error configuring network key");

            Console.WriteLine("Setting Channel ID...");
            if (channel0.setChannelID(USER_DEVICENUM, false, USER_DEVICETYPE, USER_TRANSTYPE, 500))  // Not using pairing bit
            	Console.WriteLine("Channel ID: " + channel0.getChannelNum());
            else
                throw new Exception("Error configuring Channel ID");

            genericControllableDevice = new GenericControllableDevice(channel0, networkAntPlus);
            genericControllableDevice.DataPageReceived += GenericControllableDevice_DataPageReceived;
            genericControllableDevice.TurnOn();
            
        }
        
        private static void GenericControllableDevice_DataPageReceived(DataPage arg1)
        {
              Console.WriteLine("DataPageReceived");
        }
        
        
		////////////////////////////////////////////////////////////////////////////////
        // ChannelResponse
        //
        // Called whenever a channel event is received.
        //
        // response: ANT message
        ////////////////////////////////////////////////////////////////////////////////
        static void ChannelResponse(ANT_Response response)
        {
        	//Console.WriteLine("Processing Channel Response: " + response.responseID);
        	Random rnd = new Random();

            try
            {
                switch ((ANT_ReferenceLibrary.ANTMessageID)response.responseID)
                {
                    case ANT_ReferenceLibrary.ANTMessageID.RESPONSE_EVENT_0x40:
                        {
                            switch (response.getChannelEventCode())
                            {
                                // This event indicates that a message has just been
                                // sent over the air. We take advantage of this event to set
                                // up the data for the next message period.
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TX_0x03:
                                    {
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_RX_SEARCH_TIMEOUT_0x01:
                                    {
                                        Console.WriteLine("Search Timeout");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_RX_FAIL_0x02:
                                    {
                                        Console.WriteLine("Rx Fail");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TRANSFER_RX_FAILED_0x04:
                                    {
                                        Console.WriteLine("Burst receive has failed");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TRANSFER_TX_COMPLETED_0x05:
                                    {
                                        Console.WriteLine("Transfer Completed");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TRANSFER_TX_FAILED_0x06:
                                    {
                                        Console.WriteLine("Transfer Failed");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_CHANNEL_CLOSED_0x07:
                                    {
                                        // This event should be used to determine that the channel is closed.
                                        Console.WriteLine("Channel Closed");
                                        Console.WriteLine("Unassigning Channel...");
                                        if (channel0.unassignChannel(500))
                                        {
                                            Console.WriteLine("Unassigned Channel");
                                            //Console.WriteLine("Press enter to exit");
                                            bDone = true;
                                        }
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_RX_FAIL_GO_TO_SEARCH_0x08:
                                    {
                                        Console.WriteLine("Go to Search");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_CHANNEL_COLLISION_0x09:
                                    {
                                        Console.WriteLine("Channel Collision");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TRANSFER_TX_START_0x0A:
                                    {
                                        Console.WriteLine("Burst Started");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.NO_EVENT_0x00:
                                    {
                                        Console.WriteLine("No_Event_0x00 - " + response.getChannelEventCode());
                                        break;
                                    }

                               default:
                                    {
                                        Console.WriteLine("Unhandled Channel Event " + response.getChannelEventCode());
                                        break;
                                    }
                            }
                            break;
                        }
                    case ANT_ReferenceLibrary.ANTMessageID.BROADCAST_DATA_0x4E:
                    case ANT_ReferenceLibrary.ANTMessageID.ACKNOWLEDGED_DATA_0x4F:
                    case ANT_ReferenceLibrary.ANTMessageID.BURST_DATA_0x50:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_BROADCAST_DATA_0x5D:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_ACKNOWLEDGED_DATA_0x5E:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_BURST_DATA_0x5F:
                        {
                			// Process received messages here
                            Console.WriteLine("Channel - ResponseID: " + response.responseID);
                            //Console.WriteLine("Channel - MessageID: " + response.getMessageID());                        
                            byte[] Payload = new byte[8];
                            Payload = response.getDataPayload();
                            Console.WriteLine("Channel - Payload[6]: " + Payload[6]);
                            Console.WriteLine("Channel - Payload[7]: " + Payload[7]);
                            // Command Number: 0-65535
                            int CommandNumber = 0;
                            CommandNumber = BitConverter.ToUInt16(Payload,6);
                            Console.WriteLine("Channel - CommandNumber: " + CommandNumber);

							string processName = "ZwiftApp";
							Process[] targetProcess = Process.GetProcessesByName(processName);
							if (targetProcess.Length > 0)
							{
								Console.WriteLine(processName + " found");
								Process p = targetProcess[0];
								IntPtr h = p.MainWindowHandle;
    							SetForegroundWindow(h);

			   				    INPUT[] inputs = new INPUT[1];
							   	KEYBDINPUT kb = new KEYBDINPUT();
								uint result;

								inputs[0].type = 1; //keyboard

								// Prepare Keyboard Entry !

								kb.time = 0;
   								kb.dwExtraInfo = IntPtr.Zero;
   								kb.wVk = (ushort)0x00;
								
   								
   								//
								// ANT command codes:
								//
								// 32768 - Down
								// 32769 - Up
								// 32770 - Right
								// 32771 - Left
								// 32772 - SpaceBar
								// 32773 - Enter
								// 32774 - G
								// 32775 - ESC
								// 32776 - Snapshot
								// 32777 - SwitchView
								// 32778 - ElbowFlick
								//

   								ushort key_wScan = 0x50;
    	    					uint key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);

   								switch (CommandNumber)
   								{
   									case 32768:
   										{
   											key_wScan = 0x50; // Down + Extended (E0)
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode | KeyEventF.ExtendedKey);
   											break;
   										}
   									case 32769:
   										{
   											key_wScan = 0x48; // Up + Extended (E0)
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode | KeyEventF.ExtendedKey);
   											break;
   										}
   									case 32770:
   										{
   											key_wScan = 0x4d; // Right + Extended (E0)
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode | KeyEventF.ExtendedKey);
   											break;
   										}
   									case 32771:
   										{
   											key_wScan = 0x4b; // Left + Extended (E0)
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode | KeyEventF.ExtendedKey);
   											break;
   										}
   									case 32772:
   										{
   											key_wScan = 0x39; // SpaceBar
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32773:
   										{
   											key_wScan = 0x1c; // Enter
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32774:
   										{
   											key_wScan = 0x22; // G
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32775:
   										{
   											key_wScan = 0x01; // ESC
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32776:
   										{
   											key_wScan = 0x44; // F10 - Snapshot
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32778:
   										{
   											key_wScan = 0x3b; // F1 - ElbowFlick
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}

   									case 32780:
   										{
   											key_wScan = 0x52; // Num 0 - View 0
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32781:
   										{
   											key_wScan = 0x4f; // Num 1 - View 1
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32782:
   										{
   											key_wScan = 0x50; // Num 2 - View 2
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32783:
   										{
   											key_wScan = 0x51; // Num  3 - View 3
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32784:
   										{
   											key_wScan = 0x4b; // Num 4 - View 4
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32785:
   										{
   											key_wScan = 0x4c; // Num 5 - View 5
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32786:
   										{
   											key_wScan = 0x4d; // Num 6 - View 6
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32787:
   										{
   											key_wScan = 0x47; // Num 7 - View 7
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32788:
   										{
   											key_wScan = 0x48; // Num 8 - View 8
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}
   									case 32789:
   										{
   											key_wScan = 0x49; // Num 9 - View 9
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}

   									default:
   										{
   											break;
   										}
   								}
   								
   								//kb.wScan = 0; // hardware scan code for key

   								//kb.wScan = 0x01; // Esc
   					
   								//kb.wScan = 0x10; // A = Paired Device
   								//kb.wScan = 0x12; // E - Select your Workout
   								//kb.wScan = 0x14; // T = User Customisation
   								//kb.wScan = 0x22; // G - HR-Power Graph
   								//kb.wScan = 0x44; // F10 = Screenshot

   								//kb.wScan = 0x47; // 7 Numeric
   								//kb.wScan = 0x48; // 8 Numeric
   								//kb.wScan = 0x49; // 9 Numeric
   								//kb.wScan = 0x4B; // 4 Numeric
   								//kb.wScan = 0x4C; // 5 Numeric
   								//kb.wScan = 0x4D; // 6 Numeric
   								//kb.wScan = 0x4F; // 1 Numeric
   								//kb.wScan = 0x50; // 2 Numeric
   								//kb.wScan = 0x51; // 3 Numeric
   								//kb.wScan = 0x52; // 0 Numeric

   								kb.wScan = key_wScan;
   								kb.dwFlags = key_dwFlags;
   								
   								//kb.wScan = 0x32; // M

   								//kb.dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   								//kb.dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode | KeyEventF.ExtendedKey);

							   	inputs[0].ki = kb;

							   	result = SendInput(1, inputs, Marshal.SizeOf(inputs[0]));
			   					Console.WriteLine("Result = " + result);
                            }
							else
							{
                            		Console.WriteLine("Zwift Application not found");
							}

                		}
                        break;
                    default:
                        {
                            Console.WriteLine("Unknown Message " + response.responseID);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Channel response processing failed with exception: " + ex.Message);
            }
        }


        ////////////////////////////////////////////////////////////////////////////////
        // DeviceResponse
        //
        // Called whenever a message is received from ANT unless that message is a
        // channel event message.
        //
        // response: ANT message
        ////////////////////////////////////////////////////////////////////////////////
        static void DeviceResponse(ANT_Response response)
        {
        	//Console.WriteLine("Processing Device Response: " + response.responseID );
        	switch ((ANT_ReferenceLibrary.ANTMessageID)response.responseID)
            {
                case ANT_ReferenceLibrary.ANTMessageID.STARTUP_MESG_0x6F:
                    {
                        Console.Write("RESET Complete, reason: ");

                        byte ucReason = response.messageContents[0];

                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_POR_0x00)
                            Console.WriteLine("RESET_POR");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_RST_0x01)
                            Console.WriteLine("RESET_RST");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_WDT_0x02)
                            Console.WriteLine("RESET_WDT");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_CMD_0x20)
                            Console.WriteLine("RESET_CMD");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_SYNC_0x40)
                            Console.WriteLine("RESET_SYNC");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_SUSPEND_0x80)
                            Console.WriteLine("RESET_SUSPEND");
                        break;
                    }
                case ANT_ReferenceLibrary.ANTMessageID.VERSION_0x3E:
                    {
                        Console.WriteLine("VERSION: " + new ASCIIEncoding().GetString(response.messageContents));
                        break;
                    }
                case ANT_ReferenceLibrary.ANTMessageID.RESPONSE_EVENT_0x40:
                    {
                        switch (response.getMessageID())
                        {
                            case ANT_ReferenceLibrary.ANTMessageID.CLOSE_CHANNEL_0x4C:
                                {
                                    if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.CHANNEL_IN_WRONG_STATE_0x15)
                                    {
                                        Console.WriteLine("Channel is already closed");
                                        Console.WriteLine("Unassigning Channel...");
                                        if (channel0.unassignChannel(500))
                                        {
                                            Console.WriteLine("Unassigned Channel");
                                            //Console.WriteLine("Press enter to exit");
                                            bDone = true;
                                        }
                                    }
                                    break;
                                }
                            case ANT_ReferenceLibrary.ANTMessageID.NETWORK_KEY_0x46:
                            case ANT_ReferenceLibrary.ANTMessageID.ASSIGN_CHANNEL_0x42:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_ID_0x51:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_RADIO_FREQ_0x45:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_MESG_PERIOD_0x43:
                            case ANT_ReferenceLibrary.ANTMessageID.OPEN_CHANNEL_0x4B:
                            case ANT_ReferenceLibrary.ANTMessageID.UNASSIGN_CHANNEL_0x41:
                                {
                                    if (response.getChannelEventCode() != ANT_ReferenceLibrary.ANTEventID.RESPONSE_NO_ERROR_0x00)
                                    {
                                        Console.WriteLine(String.Format("Error {0} configuring {1}", response.getChannelEventCode(), response.getMessageID()));
                                    }
                                    break;
                                }
                            case ANT_ReferenceLibrary.ANTMessageID.RX_EXT_MESGS_ENABLE_0x66:
                                {
                                    if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.INVALID_MESSAGE_0x28)
                                    {
                                        Console.WriteLine("Extended messages not supported in this ANT product");
                                        break;
                                    }
                                    else if (response.getChannelEventCode() != ANT_ReferenceLibrary.ANTEventID.RESPONSE_NO_ERROR_0x00)
                                    {
                                        Console.WriteLine(String.Format("Error {0} configuring {1}", response.getChannelEventCode(), response.getMessageID()));
                                        break;
                                    }
                                    Console.WriteLine("Extended messages enabled");
                                    break;
                                }
                            case ANT_ReferenceLibrary.ANTMessageID.REQUEST_0x4D:
                                {
                                    if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.INVALID_MESSAGE_0x28)
                                    {
                                        Console.WriteLine("Requested message not supported in this ANT product");
                                        break;
                                    }
                                    break;
                                }
                            default:
                                {
                                    Console.WriteLine("Unhandled response " + response.getChannelEventCode() + " to message " + response.getMessageID()); break;
                                }
                        }
                        break;
                    }
            }
        }


        static void PrintMenu()
        {
            // Print out options
            //
            Console.WriteLine("Available commands:");
            Console.WriteLine("  M - Print this menu");
            Console.WriteLine("  Q - Quit");
            //Console.WriteLine("C - Request Capabilities");
            //Console.WriteLine("V - Request Version");
            //Console.WriteLine("I - Request Channel ID");
            //Console.WriteLine("U - Request USB Descriptor");
        }
        
	}
}