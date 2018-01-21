/*
 * Created by SharpDevelop.
 */

// 
// ANT:
//
//		* MASTER = Controlable Device
//		* SLAVE = Remote Control

using ANT_Managed_Library;
using AntPlus.Profiles.Controls;
using AntPlus.Profiles.Components;
using AntPlus.Types;

using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;

//using System.Windows.Forms;
//using System.Threading;
//using WindowsInput;

namespace Zwift_CIQ_RC_Relay
{
	class Program
	{
		[DllImport ("User32.dll")]
		static extern int SetForegroundWindow(IntPtr point);

  		[DllImport("user32.dll")]
  		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);		

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
		
		static string ProgramVersion = "Version 01.03";
		
	    static bool bDone;
	    static bool bReset;
	    
	    
		static readonly byte USER_ANT_CHANNEL = 0;         	// ANT Channel to use
        static ushort USER_DEVICENUM = 0;       			// Device number
        static readonly byte USER_DEVICETYPE = 16;         	// Device type = 16 - Generic Remote Control
        static readonly byte USER_TRANSTYPE = 5;           	// Transmission type
        static readonly byte USER_RADIOFREQ = 57;          	// RF Frequency + 2400 MHz
		static readonly byte[] USER_NETWORK_KEY = { 0xB9, 0xA5, 0x21, 0xFB, 0xBD, 0x72, 0xC3, 0x45 };
        static readonly byte USER_NETWORK_NUM = 0;         	// The network key is assigned to this network number
        static readonly bool USER_PAIRINGENABLED = false;
        static readonly uint USER_RESPONSEWAITTIME = 1000;
        
        static ANT_Device device0;
        static ANT_Channel channel0;
        static GenericControllableDevice genericControllableDevice;
        static Network networkAntPlus = new Network(USER_NETWORK_NUM, USER_NETWORK_KEY, USER_RADIOFREQ);

        public static void Main(string[] args)
		{
			WriteLog("Zwift Garmin CIQ Remote Control Relay Program - " + ProgramVersion);
			try
            {
				AllocateUSBDongle();
				Start();
            }
            catch (Exception ex)
            {
                WriteLog("Program failed with exception: " + ex.Message);
                WriteLog("Enter Q to exit");
                Console.ReadLine();
            }

        }

      
		static async void Init()
        {

			try
            {
                WriteLog("Connecting to ANT USB Dongle...");
                //device0 = new ANT_Device();   // Create a device instance using the automatic constructor (automatic detection of USB device number and baud rate)

                device0.serialError += new ANT_Device.dSerialErrorHandler(SerialError);
                device0.deviceResponse += new ANT_Device.dDeviceResponseHandler(DeviceResponse);    // Add device response function to receive protocol event messages

                channel0 = device0.getChannel(USER_ANT_CHANNEL);    // Get channel from ANT device
                channel0.channelResponse += new dChannelResponseHandler(ChannelResponse);  // Add channel response function to receive channel event messages

                WriteLog("ANT USB Dongle initialization successful");
                WriteLog("ANT USB Dongle Device Number: " + device0.getOpenedUSBDeviceNum());
                WriteLog("ANT USB Dongle Serial Number: " + device0.getSerialNumber());
                USER_DEVICENUM =  (ushort) device0.getOpenedUSBDeviceNum();

                bReset = false;
            	List<Task> tasks = new List<Task>();
            	tasks.Add(Task.Factory.StartNew(() => 
            	{
					while (!bReset)
            		{
						WriteLog("Checking ANT USB Dongle...");
						Byte[] bytes = new byte[8];
						try
						{
							if (channel0.sendBroadcastData(bytes))
							{
								WriteLog("ANT USB Dongle is operationnal");
							}
							else
							{
								WriteLog("ANT USB Dongle is not operationnal");
							}
						}
			            catch (Exception ex)
			            {
								WriteLog("Problem with ANT USB Dongle...");
			            }
						System.Threading.Thread.Sleep(5000);
					}
            	}));
                await Task.WhenAll(tasks);

            }
            catch (Exception ex)
            {
				Console.WriteLine("Exception: " + ex);
            }
/*
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
*/
        }

        static void Start()
        {
            bDone = false;

            PrintMenu();
    
            try
            {
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
                        case "U":
                        case "u":
                            {
                                DisplayUSBConfiguration();
                                break;
                            }
                    	case "Q":
                        case "q":
                            {
                                // Quit
                                Console.WriteLine(DateTime.Now + " - Closing Channel");
                                bDone = true;
                                break;
                            }
                        case "A":
                        case "a":
                            {
                                // Allocate USB Dongle
                                AllocateUSBDongle();
								break;
                            }

                    	case "R":
                        case "r":
                            {
                                // Release USB Dongle
                                ReleaseUSBDongle();
								break;
                            }
                        case "S":
                        case "s":
                            {
                                // Re-Init USB Dongle
                                Byte[] bytes = new byte[8];
                                channel0.sendBroadcastData(bytes);
                                break;
                            }

                    	default:
                            {
                                break;
                            }
                    }
                    System.Threading.Thread.Sleep(0);
                }
                
                ReleaseUSBDongle();
                // Clean up ANT
                //Console.WriteLine("Disconnecting ANT USB Dongle...");
                //ANT_Device.shutdownDeviceInstance(ref device0);  // Close down the device completely and completely shut down all communication

                WriteLog("Application has completed successfully!");
                System.Threading.Thread.Sleep(1000);
               
                return;

            }
            catch (Exception ex)
            {
                throw new Exception("Error: " + ex.Message + Environment.NewLine);
            }
        }
        
        private static void ConfigureANT()
        {
            WriteLog("Configuring ANT communication...");
        	WriteLog("Resetting ANT USB Dongle...");
            device0.ResetSystem();     // Soft reset
            System.Threading.Thread.Sleep(500);    // Delay 500ms after a reset

            WriteLog("Setting ANT network key...");
            if (device0.setNetworkKey(USER_NETWORK_NUM, USER_NETWORK_KEY, 500))
                WriteLog("ANT network key setting successful");
            else
                throw new Exception("Error configuring network key");

            WriteLog("Setting Channel ID...");
            //channel0.setChannelTransmitPower(ANT_ReferenceLibrary.TransmitPower.RADIO_TX_POWER_0DB_0x03,500);
            if (channel0.setChannelID(USER_DEVICENUM, USER_PAIRINGENABLED, USER_DEVICETYPE, USER_TRANSTYPE, USER_RESPONSEWAITTIME))  // Not using pairing bit
            	WriteLog("Channel ID: " + channel0.getChannelNum());
            else
                throw new Exception("Error configuring Channel ID");

            genericControllableDevice = new GenericControllableDevice(channel0, networkAntPlus);
            genericControllableDevice.DataPageReceived += GenericControllableDevice_DataPageReceived;
            genericControllableDevice.TurnOn();
            //CheckUsbDongle();
        }
        
        private static void GenericControllableDevice_DataPageReceived(DataPage arg1)
        {
              WriteLog("ANT DataPage Received - PageNumber = " + arg1.DataPageNumber);
        }

        
        static void DeviceNotification(ANT_Device sender)
        {
        	Console.WriteLine(DateTime.Now + " - Processing DeviceNotification: ");        		
        }
        
        
        static void SerialError(ANT_Device sender, ANT_Managed_Library.ANT_Device.serialErrorCode error, bool isCritical)
        {
        	WriteLog("Processing SerialError: " + error);

        	WriteLog("Trying to recover USB ANT Dongle...");
        	
        	device0 = null;
        	channel0 = null;
        	bReset = true;
        	
        	while(device0 == null)
        	{
	        	try
	            {
        			WriteLog("Trying to connect to USB ANT Dongle...");
	        		device0 = new ANT_Device();
	        	}
	            catch (Exception ex)
	            {
	            }
				System.Threading.Thread.Sleep(1000);
        	}
        	WriteLog("USB ANT Dongle has been recovered");
        	Init();
            ConfigureANT();
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
        	//Console.WriteLine(DateTime.Now + " - Processing Channel Response: " + (ANT_ReferenceLibrary.ANTMessageID)response.responseID);
        	Random rnd = new Random();

            try
            {
                switch ((ANT_ReferenceLibrary.ANTMessageID)response.responseID)
                {
                    // 0x40 = Channel Message
                    
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
                                        Console.WriteLine(DateTime.Now + " - ANT Channel Closed");
                                        Console.WriteLine("Unassigning Channel...");
                                        if (channel0.unassignChannel(500))
                                        {
                                            Console.WriteLine("Unassigned Channel");
                                            //Console.WriteLine("Press enter to exit");
                                            //bDone = true;
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
                			
                            //Console.WriteLine("Channel - ResponseID: " + response.responseID);
                        
                            byte[] Payload = new byte[8];
                            Payload = response.getDataPayload();
                            //Console.WriteLine("Channel - Payload[6]: " + Payload[6]);
                            //Console.WriteLine("Channel - Payload[7]: " + Payload[7]);
                            // Command Number: 0-65535
                            int CommandNumber = 0;
                            CommandNumber = BitConverter.ToUInt16(Payload,6);

                            WriteLog("Received CommandNumber: " + CommandNumber);

							string processName = "ZwiftApp";
							//string processName = "notepad";
							
							Process[] targetProcess = Process.GetProcessesByName(processName);
							if (targetProcess.Length > 0)
							{
								WriteLog(processName + " found");
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
								// 32780 - 0 = View 0
								// 32781 - 1 = View 1
								// 32784 - 4 = View 4
								// 32785 - 5 = View 5
								// 32786 - 6 = View 6
								// 32787 - 7 = View 7
								// 32788 - 8 = View 8
								// 32789 - 9 = View 9
								// 32790 - Pg Up = FTP Bias Up
								// 32791 - Pg Down = FTP Bias Down
								// 32792 - Tab
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
   									case 32790:
   										{
   											key_wScan = 0x49; // PgUp + Extended (E0) - FTP Bias Up
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode | KeyEventF.ExtendedKey);
   											break;
   										}
   									case 32791:
   										{
   											key_wScan = 0x51; // PgDown + Extended (E0) - FTP Bias Down
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode | KeyEventF.ExtendedKey);
   											break;
   										}
   									case 32792:
   										{
   											key_wScan = 0x0f; // Tab - Skip Block
   											key_dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   											break;
   										}

   									default:
   										{
   											break;
   										}
   								}
   								
   								//kb.wScan = 0x01; // Esc
   					
   								//kb.wScan = 0x10; // A = Paired Device
   								//kb.wScan = 0x12; // E - Select your Workout
   								//kb.wScan = 0x14; // T = User Customisation
   								//kb.wScan = 0x22; // G - HR-Power Graph
   								//kb.wScan = 0x44; // F10 = Screenshot


   								kb.wScan = key_wScan;
   								kb.dwFlags = key_dwFlags;
   								
   								//kb.wScan = 0x32; // M

   								//kb.dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode);
   								//kb.dwFlags = (uint) (KeyEventF.KeyDown | KeyEventF.ScanCode | KeyEventF.ExtendedKey);

							   	inputs[0].ki = kb;

							   	result = SendInput(1, inputs, Marshal.SizeOf(inputs[0]));
                            }
							else
							{
                            		WriteLog("Zwift Application not found");
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

        	//Console.WriteLine("Processing Device Response: " + (ANT_ReferenceLibrary.ANTMessageID)response.responseID );

        	switch ((ANT_ReferenceLibrary.ANTMessageID)response.responseID)
            {
                case ANT_ReferenceLibrary.ANTMessageID.STARTUP_MESG_0x6F:
                    {

        				String reason = "";
                        byte ucReason = response.messageContents[0];

                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_POR_0x00) reason = "RESET_POR";
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_RST_0x01) reason = "RESET_RST";
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_WDT_0x02) reason = "RESET_WDT";
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_CMD_0x20) reason = "RESET_CMD";
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_SYNC_0x40) reason = "RESET_SYNC";
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_SUSPEND_0x80) reason = "RESET_SUSPEND";
                        WriteLog("RESET Complete, reason: " + reason);
                        break;
                    }
                case ANT_ReferenceLibrary.ANTMessageID.VERSION_0x3E:
                    {
                        Console.WriteLine("VERSION: " + new ASCIIEncoding().GetString(response.messageContents));
                        break;
                    }
                case ANT_ReferenceLibrary.ANTMessageID.RESPONSE_EVENT_0x40:
                    {

        				// Console.WriteLine("getMessageID: " + (ANT_ReferenceLibrary.ANTMessageID)response.getMessageID() );
						WriteLog(String.Format("Chanel Event Code = {0} - MessageID = {1}", response.getChannelEventCode(), response.getMessageID()));

						switch (response.getMessageID())
                        {
        					case ANT_ReferenceLibrary.ANTMessageID.BROADCAST_DATA_0x4E:
        						{
        							switch(response.getChannelEventCode())
        							{
        								case ANT_ReferenceLibrary.ANTEventID.CHANNEL_IN_WRONG_STATE_0x15:
        									{
        										break;
        									}
        								default:
        									{
												WriteLog(String.Format("Chanel Event Code {0} MessageID {1}", response.getChannelEventCode(), response.getMessageID()));
												break;
        									}
        							}
        							break;
        						}
        						
        						
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
                                            //bDone = true;
                                        }
                                    }
                                    break;
                                }
                            case ANT_ReferenceLibrary.ANTMessageID.UNASSIGN_CHANNEL_0x41:
        					case ANT_ReferenceLibrary.ANTMessageID.ASSIGN_CHANNEL_0x42:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_MESG_PERIOD_0x43:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_RADIO_FREQ_0x45:
        					case ANT_ReferenceLibrary.ANTMessageID.NETWORK_KEY_0x46:
                            case ANT_ReferenceLibrary.ANTMessageID.OPEN_CHANNEL_0x4B:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_ID_0x51:
        					case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_RADIO_TX_POWER_0x60:
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
            Console.WriteLine(" M - Menu");
            Console.WriteLine(" A - Allocate USB ANT Dongle");
            Console.WriteLine(" R - Release USB ANT Dongle");
            Console.WriteLine(" U - USB Configuration");
            Console.WriteLine(" Q - Quit");
            //Console.WriteLine("C - Request Capabilities");
            //Console.WriteLine("V - Request Version");
            //Console.WriteLine("I - Request Channel ID");
        }

        static void DisplayUSBConfiguration()
        {
			WriteLog("ANT USB Dongle - Device Number: " + device0.getOpenedUSBDeviceNum());
			WriteLog("ANT USB Dongle - Serial Number: " + device0.getSerialNumber());
        }
        
        static void ReleaseUSBDongle()
        {
        	WriteLog("Releasing USB ANT Dongle...");
        	try
        	{
	        	channel0.closeChannel();
				System.Threading.Thread.Sleep(1000);
				device0.ResetSystem();
				device0.ResetUSB();
				ANT_Device.shutdownDeviceInstance(ref device0);
        	}
            catch (Exception ex)
            {
                WriteLog("USB ANT Dongle is already released !");
            }
        }

        static void AllocateUSBDongle()
        {
        	WriteLog("Allocating USB ANT Dongle...");
        	try
        	{
				device0 = new ANT_Device();
				Init();
	            ConfigureANT();
        	}
            catch (Exception ex)
            {
                WriteLog("USB ANT Dongle is already allocated !");
            }
        }

        
        static void WriteLog(String message)
        {
			Console.WriteLine(DateTime.Now + " - " + message);
        }
        
	}
}