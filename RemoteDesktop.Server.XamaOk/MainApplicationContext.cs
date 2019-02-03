﻿using RemoteDesktop.Android.Core;
using RemoteDesktop.Server.XamaOK;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using WindowsInput;
using WindowsInput.Native;
using OpenH264.Encoder;
using NAudio.MediaFoundation;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RemoteDesktop.Server
{
	public class MainApplicationContext : ApplicationContext
	{
		private bool isDisposed;
		private NotifyIcon trayIcon;
		private DataSocket socket;
		private Rectangle screenRect;
		private Bitmap bitmap, scaledBitmap;
		private Graphics graphics, scaledGraphics;
        //System.Drawing.Imaging.PixelFormat format = System.Drawing.Imaging.PixelFormat.Format16bppRgb565;
        System.Drawing.Imaging.PixelFormat format = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
        //System.Drawing.Imaging.PixelFormat format = System.Drawing.Imaging.PixelFormat.Format32bppRgb;
        int screenIndex, currentScreenIndex;
        float targetFPS = 1.0f;
        float fixedTargetFPS = 1.0f;
        bool compress = false; //, currentCompress;
        bool isFixedParamUse = true; // use server side hard coded parameter on running
        bool fixedCompress = false;
        float resolutionScale = 1.0F; // DEBUG INFO: current jpeg encoding implementation is not work with not value 1.0
        float fixedResolutionScale = 0.5F;
		private System.Windows.Forms.Timer timer = null;
		public static Dispatcher dispatcher;

		//private InputSimulator input;
        private bool receivedMetaData = false;
		//private byte inputLastMouseState;
        private CaptureSoundStreamer cap_streamer;

        private ExtractedH264Encoder encoder;
        private int timestamp = 0; // equal frame number
        private int aac_adts_frame_cnt = 1;
        //public static long aac_encoding_start = 0;

        private string ffmpegPath = "C:\\Program Files\\ffmpeg-20181231-51b356e-win64-static\\bin\\ffmpeg.exe";

        //private string ffmpegForAudioEncodeArgs = "-y -loglevel debug -f f32le -sample_fmt fltp -ar 48000 -ac 2 -i - -f s16le -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -codec:a aac -ab 12K -f adts -";
        //private string ffmpegForAudioEncodeArgs = "-y -loglevel debug -f f32le -sample_fmt fltp -ar 48000 -ac 2 -i - -f s16le -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -codec:a aac -ab 12K -bsf:a aac_adtstoasc -";

        //private string ffmpegForAudioEncodeArgs = "-y -loglevel debug -f f32le -sample_fmt fltp -ar 48000 -ac 2 -i - -f u16le -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -codec:a aac -ab 12K -f adts -";

        // dont send first 2byte header???
        //private string ffmpegForAudioEncodeArgs = "-y -loglevel debug -f f32le -ar 48000 -ac 2 -i - -f u16le -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -codec:a aac -ab 12K -f adts -";

        //private string ffmpegForAudioEncodeArgs = "-y -loglevel debug -f f32le -ar 48000 -ac 2 -i - -f u16le -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -codec:a aac -profile aac_low -aac_coder fast -q:a 0.1 -f adts -";
        private string ffmpegForAudioEncodeArgs = "-f f32le -ar 48000 -ac 2 -i - -f u16le -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -codec:a aac -profile aac_low -aac_coder fast -q:a 0.1 -f adts -";

        //private string ffmpegForAudioEncodeArgs = "-y -loglevel debug -f f32le -ar 48000 -ac 2 -i - -f u16le -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -codec:a aac -ab 12K -profile aac_low -f adts -";
        //private string ffmpegForAudioEncodeArgs = "-y -loglevel debug -f f32le -ar 48000 -ac 2 -i - -f u16le -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -codec:a aac -ab 12K -profile aac_low -";

        // send Raw PCM data
        //private string ffmpegForAudioEncodeArgs = "-y -loglevel debug -f f32le -ar 48000 -ac 2 -i - -f u8 -ar " + RTPConfiguration.SamplesPerSecond + " -ac 1 -map 0 -";

        public static Process ffmpegProc = null;
        private MemoryStream debug_ms = new MemoryStream();

        public MainApplicationContext()
		{
			// init tray icon
			var menuItems = new MenuItem[]
			{
				new MenuItem("Exit", Exit),
			};

			trayIcon = new NotifyIcon()
			{
				Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
				ContextMenu = new ContextMenu(menuItems),
				Visible = true,
				Text = "Remote Desktop Server v0.1.0"
			};

            dispatcher = Dispatcher.CurrentDispatcher;

            // 重複するコードがあとで実行されるかひとまず置いておく
            // get screen to catpure
            var screens = Screen.AllScreens;
            var screen = (screenIndex < screens.Length) ? screens[screenIndex] : screens[0];
            screenRect = screen.Bounds;

            if (RTPConfiguration.isStdOutOff)
            {
                Utils.setStdoutOff();
            }

            if (RTPConfiguration.isUseFFMPEG)
            {
                kickFFMPEG();
            }

            // 音声配信サーバ
            cap_streamer = new CaptureSoundStreamer();

            //// init input simulation
            //input = new InputSimulator();


            //// start TCP socket listen for image server
            //socket = new DataSocket(NetworkTypes.Server);
            //socket.ConnectedCallback += Socket_ConnectedCallback;
            //socket.DisconnectedCallback += Socket_DisconnectedCallback;
            //socket.ConnectionFailedCallback += Socket_ConnectionFailedCallback;
            //socket.DataRecievedCallback += Socket_DataRecievedCallback;
            //socket.StartDataRecievedCallback += Socket_StartDataRecievedCallback;
            //socket.EndDataRecievedCallback += Socket_EndDataRecievedCallback;
            //socket.Listen(IPAddress.Parse(RTPConfiguration.ServerAddress), RTPConfiguration.ImageServerPort);
        }

        // set ffmpegProc field	
        private void kickFFMPEG()	
        {	
            ProcessStartInfo startInfo = new ProcessStartInfo();	
            startInfo.UseShellExecute = false; //required to redirect standart input/output	

             // redirects on your choice	
            startInfo.RedirectStandardOutput = true;	
            startInfo.RedirectStandardError = true;	
            startInfo.RedirectStandardInput = true;	
            startInfo.CreateNoWindow = true;	
            startInfo.FileName = ffmpegPath;	

             startInfo.Arguments = ffmpegForAudioEncodeArgs;	
            //startInfo.Arguments = ffmpegForDirectStreamingArgs;	

            ffmpegProc = new Process();	
            ffmpegProc.StartInfo = startInfo;	
            // リダイレクトした標準出力・標準エラーの内容を受信するイベントハンドラを設定する	
            //ffmpegProc.OutputDataReceived += useFFMPEGOutputData;	
            ffmpegProc.ErrorDataReceived  += PrintFFMPEGErrorData;	

            ffmpegProc.Start();	

             // ffmpegが確実に起動状態になるまで待つ	
            Thread.Sleep(3000);	

             // 標準出力・標準エラーの非同期読み込みを開始する	
            //ffmpegProc.BeginOutputReadLine();	
            ffmpegProc.BeginErrorReadLine();

            var task = Task.Run(() =>
            {
                byte[] ffmpegStdout_buf = new byte[2048];
                int readedBytes = 0;
                while (!ffmpegProc.StandardOutput.EndOfStream)
                {
                    readedBytes = ffmpegProc.StandardOutput.BaseStream.Read(ffmpegStdout_buf, 0, ffmpegStdout_buf.Length);
                    //Console.WriteLine(Utils.getFormatedCurrentTime() + " DEBUG: read tabun one frames " + readedBytes.ToString() + " bytes");
                    //if(aac_adts_frame_cnt % 100 == 0)
                    //{
                    //    Console.WriteLine(Utils.getFormatedCurrentTime() + " DEBUG: current encoding speed " + ((Utils.getUnixTime() - aac_encoding_start) / (float)aac_adts_frame_cnt).ToString() + " fps");
                    //}

                    //continue;
                    //debug_ms.Write(ffmpegStdout_buf, 0, readedBytes);
                    //if (debug_ms.Length > 4 * 1024)
                    //{
                    //    Utils.saveByteArrayToFile(debug_ms.ToArray(), "F:\\work\\tmp\\ffmpeg_stdout_encoded_no_adts_option_0202_1541.aac");
                    //    Environment.Exit(0);
                    //}
                    //continue;
                    if (readedBytes > 0 && this.cap_streamer != null && this.cap_streamer._AudioOutputWriter != null)
                    {                        
                        byte[] tmp_buf = new byte[readedBytes];
                        Array.Copy(ffmpegStdout_buf, 0, tmp_buf, 0, readedBytes);
                        if (RTPConfiguration.isCheckAdtsFrameNum)
                        {
                            int frame_length = ((tmp_buf[3] & 0b11) << 11) | (tmp_buf[4] << 3) | (tmp_buf[5] >> 5);

                            // ffmpegから読みだしたstdoutに複数のrdtsフレームが含まれていた場合
                            if (frame_length != readedBytes)
                            {
                                Console.WriteLine("At ffmpeg stdout handling: data contains multi adts frames. readedBytes = " + readedBytes.ToString());
                                int cur_base_pos = 0;
                                while(cur_base_pos != readedBytes)
                                {
                                    aac_adts_frame_cnt++;
                                    frame_length = ((tmp_buf[cur_base_pos + 3] & 0b11) << 11) | (tmp_buf[cur_base_pos + 4] << 3) | (tmp_buf[cur_base_pos + 5] >> 5);
                                    Console.WriteLine("read from stdout of ffmpeg " + readedBytes + " Bytes and send the data to client inner frame " + frame_length.ToString() + " bytes. aac_adts_frame_cnt = " + aac_adts_frame_cnt.ToString());
                                    if (RTPConfiguration.isRunCapturedSoundDataHndlingWithoutConn == false)
                                    {
                                        byte[] buf = new byte[frame_length];
                                        Array.Copy(tmp_buf, cur_base_pos, buf, 0, frame_length);
                                        this.cap_streamer._AudioOutputWriter.handleDataWithTCP(buf);
                                        cur_base_pos += frame_length;
                                    }                                    
                                }

                                continue; // stdout の readへ戻る
                            }
                        }

                        aac_adts_frame_cnt++;
                        Console.WriteLine("read from stdout of ffmpeg " + readedBytes + " Bytes and send the data to client. aac_adts_frame_cnt = " + aac_adts_frame_cnt.ToString());
                        if(RTPConfiguration.isRunCapturedSoundDataHndlingWithoutConn == false)
                        {
                            this.cap_streamer._AudioOutputWriter.handleDataWithTCP(tmp_buf);
                        }
                    }
                }
            });
        }	

        
        //private void useFFMPEGOutputData(object sender, DataReceivedEventArgs e)	
        //{
        //    Process p = (Process)sender;
        //    Console.WriteLine("useFFMPEGOutputData called!");

        //    //if (!string.IsNullOrEmpty(e.Data))
        //    if (e.Data != null && e.Data.Length > 0)
        //    {
        //    }
        //}	

        private void PrintFFMPEGErrorData(object sender, DataReceivedEventArgs e)	
        {
            // 子プロセスの標準エラーから受信した内容を自プロセスの標準エラーに書き込む	
            Process p = (Process)sender;

            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Error.WriteLine("[{0}2;stderr] {1}", p.ProcessName, e.Data);
            }
        }

		void Exit(object sender, EventArgs e)
		{
			// dispose
			lock (this)
			{
				isDisposed = true;

				if (timer != null)
				{
					timer.Stop();
					timer.Tick -= Timer_Tick;
					timer.Dispose();
					timer = null;
				}

				if (socket != null)
				{
					socket.Dispose();
					socket = null;
				}

				if (graphics != null)
				{
					graphics.Dispose();
					graphics = null;
				}

				if (bitmap != null)
				{
					bitmap.Dispose();
					bitmap = null;
				}
			}

			// exit
			trayIcon.Visible = false;
			Application.Exit();
		}

		private void Socket_StartDataRecievedCallback(MetaData metaData)
		{
			lock (this)
			{
				if (isDisposed) return;

				void CreateTimer(bool recreate, int fps)
				{
					if (recreate && timer != null)
					{
                        if (RTPConfiguration.isStreamRawH264Data)
                        {
                            timer.Tick -= Timer_Tick_bitmap_to_openH264_Encoder;
                        }
                        else
                        {
                            timer.Tick -= Timer_Tick;
                        }
                        timer.Dispose();
						timer = null;
					}

					if (timer == null)
					{
						timer = new System.Windows.Forms.Timer();
                        timer.Interval = (int) (1000f / fps); // targetFPSは呼び出し時には適切に更新が行われていることを想定
                        if (!RTPConfiguration.isStreamRawH264Data)
                        {
                            timer.Tick += Timer_Tick;
                        }
					}

					timer.Start();
				}

				// update settings
				if (metaData.type == MetaDataTypes.UpdateSettings || metaData.type == MetaDataTypes.StartCapture)
				{
					DebugLog.Log("Updating settings");
                    //format = metaData.format;
                    //format = System.Drawing.Imaging.PixelFormat.Format

                    format = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
                    screenIndex = metaData.screenIndex;
					compress = metaData.compressed;
					resolutionScale = metaData.resolutionScale;
					targetFPS = metaData.targetFPS;
                    if (isFixedParamUse)
                    {
                        compress = fixedCompress;
                        targetFPS = fixedTargetFPS;
                        resolutionScale = fixedResolutionScale;
                    }
                    receivedMetaData = true;
					if (metaData.type == MetaDataTypes.UpdateSettings)
					{
						dispatcher.InvokeAsync(delegate()
						{
							CreateTimer(true, (int)targetFPS);
						});
					}
				}

				// start / stop
				if (metaData.type == MetaDataTypes.StartCapture)
				{
					dispatcher.InvokeAsync(delegate()
					{
						CreateTimer(false, (int)targetFPS);
					});
				}
				else if (metaData.type == MetaDataTypes.PauseCapture)
				{
					dispatcher.InvokeAsync(delegate()
					{
						timer.Stop();
					});
				}
				else if (metaData.type == MetaDataTypes.ResumeCapture)
				{
					dispatcher.InvokeAsync(delegate()
					{
						timer.Start();
					});
				}
				//else if (metaData.type == MetaDataTypes.UpdateMouse)
				//{
				//	// mouse pos
				//	Cursor.Position = new Point(metaData.mouseX, metaData.mouseY);

				//	// mouse clicks
				//	if (inputLastMouseState != metaData.mouseButtonPressed)
				//	{
				//		// handle state changes
				//		if (inputLastMouseState == 1) input.Mouse.LeftButtonUp();
				//		else if (inputLastMouseState == 2) input.Mouse.RightButtonUp();
				//		else if (inputLastMouseState == 3) input.Mouse.XButtonUp(2);

				//		// handle new state
				//		if (metaData.mouseButtonPressed == 1) input.Mouse.LeftButtonDown();
				//		else if (metaData.mouseButtonPressed == 2) input.Mouse.RightButtonDown();
				//		else if (metaData.mouseButtonPressed == 3) input.Mouse.XButtonDown(2);
				//	}

				//	// mouse scroll wheel
				//	if (metaData.mouseScroll != 0) input.Mouse.VerticalScroll(metaData.mouseScroll);

				//	// finish
				//	inputLastMouseState = metaData.mouseButtonPressed;
				//}
				//else if (metaData.type == MetaDataTypes.UpdateKeyboard)
				//{
				//	VirtualKeyCode specialKey = 0;
				//	if (metaData.specialKeyCode != 0)
				//	{
				//		specialKey = ConvertKeyCode((Key)metaData.specialKeyCode);
				//		if (specialKey != 0) input.Keyboard.KeyDown(specialKey);
				//	}

				//	if (metaData.keyCode != 0)
				//	{
				//		var key = ConvertKeyCode((Key)metaData.keyCode);
				//		if (key != 0) input.Keyboard.KeyPress(key);
				//		if (specialKey != 0) input.Keyboard.KeyUp(specialKey);
				//	}
				//}
			}
		}

		private void Socket_EndDataRecievedCallback()
		{
			// do nothing
		}

		private void Socket_DataRecievedCallback(byte[] data, int dataSize, int offset)
		{
			// do nothing
		}

        // only used on Client
		private void Socket_ConnectionFailedCallback(string error)
		{
			DebugLog.LogError("Failed to connect: " + error);
		}

		private void Socket_ConnectedCallback()
		{
			DebugLog.Log("Connected to client");

            if (RTPConfiguration.isStreamRawH264Data)
            {
                lock (this)
                {
                        if (isDisposed) return;
                        if (isFixedParamUse)
                        {
                            encoder = new ExtractedH264Encoder((int)(screenRect.Height * fixedResolutionScale), (int)(screenRect.Width * fixedResolutionScale), 
                                RTPConfiguration.h246EncoderBitPerSec, fixedTargetFPS, RTPConfiguration.h264EncoderKeyframeInterval);
                            encoder.encodedDataGenerated += h264RawDataHandlerSendTCP;
                        }
                        else // この時点ではクライアントから指定されたscaleは分からないので、fixedXXXXXをひとまず使っておく
                        {
                            encoder = new ExtractedH264Encoder((int)(screenRect.Height * fixedResolutionScale), (int)(screenRect.Width * fixedResolutionScale),
                                RTPConfiguration.h246EncoderBitPerSec, fixedTargetFPS, RTPConfiguration.h264EncoderKeyframeInterval);
                            encoder.encodedDataGenerated += h264RawDataHandlerSendTCP;
                        }


                        void CreateTimer(bool recreate, int fps)
                        {
                            if (timer == null)
                            {
                                timer = new System.Windows.Forms.Timer();
                                timer.Interval = (int)(1000f / fps); // targetFPSは呼び出し時には適切に更新が行われていることを想定
                                timer.Tick += Timer_Tick_bitmap_to_openH264_Encoder;
                            }

                            timer.Start();
                        }

					    dispatcher.InvokeAsync(delegate()
					    {
						    CreateTimer(false, (int)targetFPS);
					    });

                }
            }
		}

		private void Socket_DisconnectedCallback()
		{
			DebugLog.Log("Disconnected from client");
            receivedMetaData = false;
			dispatcher.InvokeAsync(delegate()
			{
                if (timer != null)
                {
                    timer.Tick -= Timer_Tick_bitmap_to_openH264_Encoder;
					timer.Dispose();
					timer = null;
                }
                if(encoder != null)
                {
                    encoder.reInit();
                }
                timestamp = 0;
				socket.ReListen();
			});
		}

        private unsafe BitmapXama convertToBitmapXamaAndRotate(Bitmap bmap)
        {
            //bmap.RotateFlip(RotateFlipType.Rotate90FlipY);
            bmap.RotateFlip(RotateFlipType.Rotate90FlipNone); // for OpenH264 encoder&decoder

            Rectangle rect = new Rectangle(0, 0, bmap.Width, bmap.Height);

            BitmapData bmpData = null;
            Bitmap bmap16 = null;
            long dataLength = -1;

            bmpData = bmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, bmap.PixelFormat);
            dataLength = bmap.Width * bmap.Height * 3; //RGB24

            
            IntPtr ptr = bmpData.Scan0;
            MemoryStream ms = new MemoryStream();
            var bitmapStream = new UnmanagedMemoryStream((byte*)bmpData.Scan0, dataLength);
            bitmapStream.CopyTo(ms);
            bmap.UnlockBits(bmpData);


            //byte[] buf = ms.GetBuffer();
            byte[] buf = ms.ToArray();
            var retBmap = new BitmapXama(buf);
            retBmap.Height = bmap.Height;
            retBmap.Width = bmap.Width;

            return retBmap;
        }

        private void h264RawDataHandlerSendTCP(byte[] data)
        {
            BitmapXama bmpXama = new BitmapXama(data);
            bmpXama.Width = screenRect.Width;
            bmpXama.Height = screenRect.Height;

            socket.SendImage(bmpXama, screenRect.Width, screenRect.Height, screenIndex, compress, targetFPS);
        }

		private void Timer_Tick_bitmap_to_openH264_Encoder(object sender, EventArgs e)
		{
			lock (this)
			{
                if (isFixedParamUse)
                {
                    resolutionScale = fixedResolutionScale;
                }

                CaptureScreen();
                if(timestamp < RTPConfiguration.initialSkipCaptureNums)
                {
                    timestamp++;
                    return;
                }

                BitmapXama convedXBmap = null;
                byte[] tmp_buf = new byte[scaledBitmap.Width * scaledBitmap.Height * 3];
                if (resolutionScale == 1)
                {
                    convedXBmap = convertToBitmapXamaAndRotate(bitmap);

                }
                else
                {
                    convedXBmap = convertToBitmapXamaAndRotate(scaledBitmap);

                }

                if (convedXBmap.getInternalBuffer().Length == 0)
                {
                    return;
                }
                Array.Copy(convedXBmap.getInternalBuffer(), 0, tmp_buf, 0, tmp_buf.Length);
                var bitmap_ms = Utils.getAddHeaderdBitmapStreamByPixcels(tmp_buf, convedXBmap.Width, convedXBmap.Height);

                Console.WriteLine("write data as bitmap file byte data to encoder " + bitmap_ms.Length.ToString() + "Bytes timestamp=" + timestamp.ToString());
                encoder.addBitmapFrame(bitmap_ms.ToArray(), timestamp - RTPConfiguration.initialSkipCaptureNums);
                timestamp++;
            }
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			lock (this)
			{
				if (isDisposed) return;
                if (!receivedMetaData) return;

				CaptureScreen();
                BitmapXama convedXBmap = null;
                if (resolutionScale == 1)
                {
                    convedXBmap = convertToBitmapXamaAndRotate(bitmap);
                    socket.SendImage(convedXBmap, screenRect.Width, screenRect.Height, screenIndex, compress, targetFPS);
                }
                else
                {
                    convedXBmap = convertToBitmapXamaAndRotate(scaledBitmap);
                    socket.SendImage(convedXBmap, screenRect.Width, screenRect.Height, screenIndex, compress, targetFPS);
                }
			}
		}

		private void CaptureScreen()
		{
            lock (this)
            {
                if (bitmap == null || bitmap.PixelFormat != format)
                {
                    currentScreenIndex = screenIndex;

                    // get screen to catpure
                    var screens = Screen.AllScreens;
                    var screen = (screenIndex < screens.Length) ? screens[screenIndex] : screens[0];
                    screenRect = screen.Bounds;
                }

                // --- avoid noised bitmap sended due to lotate of convert to BitmapXama (not good solution) ---
                // create bitmap resources
                if (bitmap != null)
                {
                    bitmap.Dispose();
                    bitmap = null;
                }
                if (graphics != null)
                {
                    graphics.Dispose();
                    graphics = null;
                }
                bitmap = new Bitmap(screenRect.Width, screenRect.Height, format);
                graphics = Graphics.FromImage(bitmap);

                float localScale = 1;
                if (resolutionScale != 1 || fixedResolutionScale != 1)
                {
                    if (scaledBitmap != null)
                    {
                        scaledBitmap.Dispose();
                        scaledBitmap = null;
                    }
                    if (scaledGraphics != null)
                    {
                        scaledGraphics.Dispose();
                        scaledGraphics = null;
                    }
                    localScale = resolutionScale;
                    if(fixedResolutionScale != 1)
                    {
                        localScale = fixedResolutionScale;
                    }
                    scaledBitmap = new Bitmap((int)(screenRect.Width * localScale), (int)(screenRect.Height * localScale), format);
                    scaledGraphics = Graphics.FromImage(scaledBitmap);
                }
                // ---                                         end                                          ---

                // capture screen
                graphics.CopyFromScreen(screenRect.Left, screenRect.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                if (localScale != 1)
                {
                    scaledGraphics.DrawImage(bitmap, 0, 0, scaledBitmap.Width, scaledBitmap.Height);
                }
            }
        }

		//private VirtualKeyCode ConvertKeyCode(Key keycode)
		//{
		//	switch (keycode)
		//	{
		//		case Key.A: return VirtualKeyCode.VK_A;
		//		case Key.B: return VirtualKeyCode.VK_B;
		//		case Key.C: return VirtualKeyCode.VK_C;
		//		case Key.D: return VirtualKeyCode.VK_D;
		//		case Key.E: return VirtualKeyCode.VK_E;
		//		case Key.F: return VirtualKeyCode.VK_F;
		//		case Key.G: return VirtualKeyCode.VK_G;
		//		case Key.H: return VirtualKeyCode.VK_H;
		//		case Key.I: return VirtualKeyCode.VK_I;
		//		case Key.J: return VirtualKeyCode.VK_J;
		//		case Key.K: return VirtualKeyCode.VK_K;
		//		case Key.L: return VirtualKeyCode.VK_L;
		//		case Key.M: return VirtualKeyCode.VK_M;
		//		case Key.N: return VirtualKeyCode.VK_N;
		//		case Key.O: return VirtualKeyCode.VK_O;
		//		case Key.P: return VirtualKeyCode.VK_P;
		//		case Key.Q: return VirtualKeyCode.VK_Q;
		//		case Key.R: return VirtualKeyCode.VK_R;
		//		case Key.S: return VirtualKeyCode.VK_S;
		//		case Key.T: return VirtualKeyCode.VK_T;
		//		case Key.U: return VirtualKeyCode.VK_U;
		//		case Key.V: return VirtualKeyCode.VK_V;
		//		case Key.W: return VirtualKeyCode.VK_W;
		//		case Key.X: return VirtualKeyCode.VK_X;
		//		case Key.Y: return VirtualKeyCode.VK_Y;
		//		case Key.Z: return VirtualKeyCode.VK_Z;

		//		case Key.D0: return VirtualKeyCode.VK_0;
		//		case Key.D1: return VirtualKeyCode.VK_1;
		//		case Key.D2: return VirtualKeyCode.VK_2;
		//		case Key.D3: return VirtualKeyCode.VK_3;
		//		case Key.D4: return VirtualKeyCode.VK_4;
		//		case Key.D5: return VirtualKeyCode.VK_5;
		//		case Key.D6: return VirtualKeyCode.VK_6;
		//		case Key.D7: return VirtualKeyCode.VK_7;
		//		case Key.D8: return VirtualKeyCode.VK_8;
		//		case Key.D9: return VirtualKeyCode.VK_9;

		//		case Key.NumPad0: return VirtualKeyCode.NUMPAD0;
		//		case Key.NumPad1: return VirtualKeyCode.NUMPAD1;
		//		case Key.NumPad2: return VirtualKeyCode.NUMPAD2;
		//		case Key.NumPad3: return VirtualKeyCode.NUMPAD3;
		//		case Key.NumPad4: return VirtualKeyCode.NUMPAD4;
		//		case Key.NumPad5: return VirtualKeyCode.NUMPAD5;
		//		case Key.NumPad6: return VirtualKeyCode.NUMPAD6;
		//		case Key.NumPad7: return VirtualKeyCode.NUMPAD7;
		//		case Key.NumPad8: return VirtualKeyCode.NUMPAD8;
		//		case Key.NumPad9: return VirtualKeyCode.NUMPAD9;

		//		case Key.Subtract: return VirtualKeyCode.SUBTRACT;
		//		case Key.Add: return VirtualKeyCode.ADD;
		//		case Key.Multiply: return VirtualKeyCode.MULTIPLY;
		//		case Key.Divide: return VirtualKeyCode.DIVIDE;
		//		case Key.Decimal: return VirtualKeyCode.DECIMAL;

		//		case Key.F1: return VirtualKeyCode.F1;
		//		case Key.F2: return VirtualKeyCode.F2;
		//		case Key.F3: return VirtualKeyCode.F3;
		//		case Key.F4: return VirtualKeyCode.F4;
		//		case Key.F5: return VirtualKeyCode.F5;
		//		case Key.F6: return VirtualKeyCode.F6;
		//		case Key.F7: return VirtualKeyCode.F7;
		//		case Key.F8: return VirtualKeyCode.F8;
		//		case Key.F9: return VirtualKeyCode.F9;
		//		case Key.F10: return VirtualKeyCode.F10;
		//		case Key.F11: return VirtualKeyCode.F11;
		//		case Key.F12: return VirtualKeyCode.F12;

		//		case Key.LeftShift: return VirtualKeyCode.LSHIFT;
		//		case Key.RightShift: return VirtualKeyCode.RSHIFT;
		//		case Key.LeftCtrl: return VirtualKeyCode.LCONTROL;
		//		case Key.RightCtrl: return VirtualKeyCode.RCONTROL;
		//		case Key.LeftAlt: return VirtualKeyCode.LMENU;
		//		case Key.RightAlt: return VirtualKeyCode.RMENU;

		//		case Key.Back: return VirtualKeyCode.BACK;
		//		case Key.Space: return VirtualKeyCode.SPACE;
		//		case Key.Return: return VirtualKeyCode.RETURN;
		//		case Key.Tab: return VirtualKeyCode.TAB;
		//		case Key.CapsLock: return VirtualKeyCode.CAPITAL;
		//		case Key.Oem1: return VirtualKeyCode.OEM_1;
		//		case Key.Oem2: return VirtualKeyCode.OEM_2;
		//		case Key.Oem3: return VirtualKeyCode.OEM_3;
		//		case Key.Oem4: return VirtualKeyCode.OEM_4;
		//		case Key.Oem5: return VirtualKeyCode.OEM_5;
		//		case Key.Oem6: return VirtualKeyCode.OEM_6;
		//		case Key.Oem7: return VirtualKeyCode.OEM_7;
		//		case Key.Oem8: return VirtualKeyCode.OEM_8;
		//		case Key.OemComma: return VirtualKeyCode.OEM_COMMA;
		//		case Key.OemPeriod: return VirtualKeyCode.OEM_PERIOD;
		//		case Key.Escape: return VirtualKeyCode.ESCAPE;

		//		case Key.Home: return VirtualKeyCode.HOME;
		//		case Key.End: return VirtualKeyCode.END;
		//		case Key.PageUp: return VirtualKeyCode.PRIOR;
		//		case Key.PageDown: return VirtualKeyCode.NEXT;
		//		case Key.Insert: return VirtualKeyCode.INSERT;
		//		case Key.Delete: return VirtualKeyCode.DELETE;

		//		case Key.Left: return VirtualKeyCode.LEFT;
		//		case Key.Right: return VirtualKeyCode.RIGHT;
		//		case Key.Down: return VirtualKeyCode.DOWN;
		//		case Key.Up: return VirtualKeyCode.UP;

		//		default: return 0;
		//	}
		//}
	}
}
