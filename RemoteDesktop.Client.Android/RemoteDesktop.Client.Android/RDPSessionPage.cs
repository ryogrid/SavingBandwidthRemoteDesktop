using System;
using System.Collections.Generic;
using Xamarin.Forms;

using RemoteDesktop.Android.Core;
using System.Threading;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.IO.Compression;
using Xamarin.Forms.Xaml;
using SkiaSharp.Views.Forms;
using SkiaSharp;
using System.Runtime.InteropServices;
using RemoteDesktop.Android.Core;

namespace RemoteDesktop.Client.Android
{

    enum BITMAP_DISPLAY_COMPONENT_TAG
    {
        COMPONENT_1,
        COMPONENT_2,
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public class RDPSessionPage : ContentPage
    {
        private DataSocket socket;
        private MetaData metaData;
        private MemoryStream compressedStream;
        private bool isDisposed;

        public static int width = 432; // dp based app display area size is set
        public static int height = 708; // dp based app display area size is set

        private TCPSoundStreamPlayer player = null;
        private AbsoluteLayout layout;

        private BITMAP_DISPLAY_COMPONENT_TAG curUpdateTargetComoonentOrBuf = BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_2;
        private bool isAppDisplaySizeGot = false;
        private bool isBitDisplayComponetsAdded = false;
        private int totalDisplayedFrames = 0;

        public SKCanvasView canvas = null;
        private MemoryStream[] skiaBufStreams;

        private VideoDecoderManager vdecoder = null;

        private Queue<byte[]> encodedFrameDataQ = new Queue<byte[]>();
        private DecoderCallback decoCallback;

        private int h264DecodedHeight = -1;
        private int h264DecodedWidth = -1;
        private int h264DecodedPixFmt = -1;

        public int pcScreenWidth = -1;
        public int pcScreenHeight = -1;
        public int skiaCanvasWidth = -1;
        public int skiaCanvasHeight = -1;

        private InputManager input;

        public RDPSessionPage()
        {
            NavigationPage.SetHasNavigationBar(this, false);

            //InitializeComponent();

            if (GlobalConfiguration.isStdOutOff)
            {
                Utils.setStdoutOff();
            }

            if (GlobalConfiguration.isEnableImageStreaming || GlobalConfiguration.isEnableInputDeviceController)
            {
                canvas = new SKCanvasView
                {
                    //VerticalOptions = LayoutOptions.FillAndExpand
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill
                };
                canvas.PaintSurface += OnCanvasViewPaintSurface;
                // Skiaを利用する場合、ビットマップデータのバッファはここで用意してしまう
                skiaBufStreams = new MemoryStream[2];
                skiaBufStreams.SetValue(new MemoryStream(), 0);
                skiaBufStreams.SetValue(new MemoryStream(), 1);
            }

            Title = "RDPSession";
            layout = new AbsoluteLayout();
            Content = layout;

            socket = new DataSocket(NetworkTypes.Client);

            if (GlobalConfiguration.isEnableSoundStreaming && !GlobalConfiguration.isClientRunWithoutConn) connectToSoundServer(); // start recieve sound data which playing on remote PC
            if (GlobalConfiguration.isEnableInputDeviceController) setupInputManager();

            if ((GlobalConfiguration.isEnableImageStreaming || GlobalConfiguration.isEnableInputDeviceController) && !GlobalConfiguration.isClientRunWithoutConn)
            {
                socket.ConnectedCallback += Socket_ConnectedCallback;
                socket.DisconnectedCallback += Socket_DisconnectedCallback;
                socket.ConnectionFailedCallback += Socket_ConnectionFailedCallback;
                socket.DataRecievedCallback += Socket_DataRecievedCallback;
                socket.StartDataRecievedCallback += Socket_StartDataRecievedCallback;
                socket.EndDataRecievedCallback += Socket_EndDataRecievedCallback;
                socket.Connect(IPAddress.Parse(GlobalConfiguration.ServerAddress), GlobalConfiguration.ImageAndInputServerPort);
            }

        }

        void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            SKImageInfo info = args.Info;
            SKSurface surface = args.Surface;
            SKCanvas canvas = surface.Canvas;

            if (skiaCanvasWidth == -1)
            {
                skiaCanvasWidth = info.Width;
                skiaCanvasHeight = info.Height;
                return; //一回目は空打ちなのでreturnする
            }

            if (h264DecodedWidth != -1)
            {
                float original_height = -1;
                float original_width = -1;
                if (h264DecodedHeight != -1)
                {
                    original_width = h264DecodedWidth;
                    original_height = h264DecodedHeight;
                }
                else
                {
                    original_width = metaData.width;
                    original_height = metaData.height;
                }

                if (!isBitDisplayComponetsAdded)
                {
                    return;
                }

                float fit_width = original_width;
                float fit_height = original_height;
                float x_ratio = info.Width / (float)original_width;
                float y_ratio = info.Height / (float)original_height;
                if (x_ratio < y_ratio)
                {
                    fit_width *= x_ratio;
                    fit_height *= x_ratio;
                }
                else
                {
                    fit_width *= y_ratio;
                    fit_height *= y_ratio;
                }

                SKRect destRect = new SKRect(info.Width - fit_width, 0, fit_width, fit_height);
                SKRect sourceRect = new SKRect(0, 0, original_width, original_height);


                byte[] bitmap_data = null;
                long dataLength = -1;
                // curUpdateTargetComoonentOrBuf は既に更新中のものになっているはずなので、以下はその前提
                if (curUpdateTargetComoonentOrBuf == BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_1)
                {
                    bitmap_data = skiaBufStreams[1].ToArray();
                    dataLength = skiaBufStreams[1].Length;
                    skiaBufStreams[1].Position = 0;
                }
                else
                {
                    bitmap_data = skiaBufStreams[0].ToArray();
                    dataLength = skiaBufStreams[0].Length;
                    skiaBufStreams[0].Position = 0;
                }
                if (dataLength == 0)
                {
                    return;
                }

                byte[] conved_bmp_data = null;
                if (h264DecodedPixFmt == 21) // yuv420 semi planar (nv12)
                {
                    conved_bmp_data = Utils.NV12ToRGBA8888(bitmap_data, (int)original_width, (int)original_height);
                }
                else if (h264DecodedPixFmt == 19) // yuv420 planar (yv12)
                {
                    conved_bmp_data = Utils.YV12ToRGBA8888(bitmap_data, (int)original_width, (int)original_height);
                }

                GCHandle gcHandle = GCHandle.Alloc(conved_bmp_data, GCHandleType.Pinned);

                var skinfo = new SKImageInfo((int)original_width, (int)original_height, SKColorType.Rgba8888, SKAlphaType.Opaque);

                SKBitmap skbitmap = new SKBitmap();
                skbitmap.InstallPixels(skinfo, gcHandle.AddrOfPinnedObject(), skinfo.RowBytes, delegate { gcHandle.Free(); }, null);

                // Display the bitmap
                canvas.Clear();
                canvas.Scale(1, -1, 0, info.Height / 2);
                canvas.DrawBitmap(skbitmap, sourceRect, destRect);
                if (input != null)
                {
                    int[] xy_arr;
                    if ((xy_arr = input.getCursorInternalCursorPos()) != null)
                    {
                        Console.WriteLine("Draw dummy cursor!");
                        var paint = new SKPaint
                        {
                            Color = new SKColor(255, 0, 0),
                            Style = SKPaintStyle.Fill
                        };
                        canvas.DrawCircle(xy_arr[0], xy_arr[1], 10.0f, paint);
                    }
                }

                Console.WriteLine("double_image: canvas size =" + info.Width.ToString() + "x" + info.Height.ToString() + " scaled image size =" + fit_width.ToString() + "x" + fit_height.ToString());
            }
        }

        protected override void OnDisappearing()
        {
            DisposePageHavingResources();
        }

        private void DisposePageHavingResources()
        {
            if (player != null)
            {
                // サウンド回りの終了処理はこの呼び出しで全て行われる
                player.togglePlayingTCP();
            }
            if (socket != null)
            {
                // input server および image server との通信に利用しているソケットの終了処理
                Device.BeginInvokeOnMainThread(() =>
                {
                    if (socket != null) socket.Dispose();
                    socket = null;
                });
            }
            if (vdecoder != null)
            {
                vdecoder.Close();
                vdecoder = null;
            }
        }
        public void connectToSoundServer()
        {
            player = new TCPSoundStreamPlayer();
            player.togglePlayingTCP();
        }

        protected override bool OnBackButtonPressed()
        {
            base.OnBackButtonPressed();

            //updateImageContentRandom();
            return true;
        }

        private void addBitmatDisplayComponentToLayout()
        {
            layout.Children.Add(canvas, new Rectangle(0, 0, width, height));

            isBitDisplayComponetsAdded = true;
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            Console.WriteLine("OnSizeAllocated: " + width.ToString() + "x" + height.ToString());

            RDPSessionPage.width = (int)width;
            RDPSessionPage.height = (int)height;
            if ((GlobalConfiguration.isEnableImageStreaming || GlobalConfiguration.isEnableInputDeviceController) && !isAppDisplaySizeGot)
            {
                addBitmatDisplayComponentToLayout();
            }
            if (input != null && !input.isAddedGestureViewLayer)
            {
                input.addGestureViewLayer();
                input.isAddedGestureViewLayer = true;
            }
            this.isAppDisplaySizeGot = true;
        }

        private void setupInputManager()
        {
            // handle connect
            input = new InputManager(socket, layout, this);
        }

        private void H264DecodedDataHandler(byte[] decoded_data, int width, int height, int pix_fmt)
        {
            Task.Run(() =>
            {
                h264DecodedWidth = width;
                h264DecodedHeight = height;
                h264DecodedPixFmt = pix_fmt;
                if (curUpdateTargetComoonentOrBuf == BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_1)
                {
                    skiaBufStreams[0].Write(decoded_data, 0, decoded_data.Length);
                }
                else
                {
                    skiaBufStreams[1].Write(decoded_data, 0, decoded_data.Length);
                }
                // このメソッドの中でImageコンポーネントへの更新通知も行う
                dataUpdateTargetImageComponentToggle();
            });
        }


        // Imageコンポーネントへのデータ更新通知もここで行う
        private void dataUpdateTargetImageComponentToggle()
        {
            Console.WriteLine("double_image: updateTarget=" + curUpdateTargetComoonentOrBuf.ToString() + " @ start of dataUpdateTargetImageComponentToggle");
            if (curUpdateTargetComoonentOrBuf == BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_1)
            {
                Console.WriteLine("double_image: rerender canvas, target <- COMPONENT2 @ dataUpdateTargetImageComponentToggle");
                curUpdateTargetComoonentOrBuf = BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_2;
            }
            else
            {
                Console.WriteLine("double_image: rerender canvas, target <- COMPONENT1 @ dataUpdateTargetImageComponentToggle");
                curUpdateTargetComoonentOrBuf = BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_1;
            }
            Console.WriteLine("double_image: call canvas.InvalidateSurface");

            Device.BeginInvokeOnMainThread(() =>
            {
                canvas.InvalidateSurface();
            });
        }

        private void Socket_StartDataRecievedCallback(MetaData metaData)
        {
            if (metaData.type != MetaDataTypes.ImageData) throw new Exception("Invalid meta data type: " + metaData.type);

            Console.WriteLine("recieved MetaData @ StartDataRecievedCallback");

            //Imageコンポーネントの差し替えにともなってバッファアドレスが変わるかもしれないので
            //待つ
            var tcs = new TaskCompletionSource<bool>();
            Task.Run(() =>
            {
                lock (this)
                {
                    this.metaData = metaData;
                    if (pcScreenWidth == -1)
                    {
                        pcScreenWidth = metaData.screenWidth;
                        pcScreenHeight = metaData.screenHeight;
                    }

                    if (isAppDisplaySizeGot == false)
                    {
                        return;
                    }

                    try
                    {

                        // init compression
                        if (metaData.compressed || GlobalConfiguration.isStreamRawH264Data)
                        {
                            if (compressedStream == null)
                            {
                                compressedStream = new MemoryStream();
                            }
                            else
                            {
                                compressedStream.SetLength(0);
                            }
                        }
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        tcs.SetException(ex);
                    }
                    Utils.startTimeMeasure("Image_Transfer_Communication");
                }
            });
            var task = tcs.Task;
            try
            {
                task.Wait();
            }
            catch { }
        }

        private void Socket_EndDataRecievedCallback()
        {
            //Utils.startTimeMeasure("Image_Update");
            //var tcs = new TaskCompletionSource<bool>();
            Task.Run(() =>
            {
                lock (this)
                {
                    Console.WriteLine("elapsed for image data transfer communication: " + Utils.stopMeasureAndGetElapsedMilliSeconds("Image_Transfer_Communication").ToString() + " msec");
                    try
                    {

                        if (GlobalConfiguration.isStreamRawH264Data)
                        {
                            byte[] encoded_buf = compressedStream.ToArray();
                            Console.Write("pass encoded data to decoder length = " + encoded_buf.Length.ToString());


                            if (decoCallback == null)
                            {
                                decoCallback = new DecoderCallback(encodedFrameDataQ);
                                decoCallback.encodedDataGenerated += H264DecodedDataHandler;
                            }

                            decoCallback.addEncodedFrameData(encoded_buf, encoded_buf.Length);

                            if (vdecoder == null)
                            {
                                vdecoder = new VideoDecoderManager();
                                vdecoder.setup(decoCallback, (int)metaData.width, (int)metaData.height);
                            }

                            return;
                        }
                        else if (GlobalConfiguration.isConvJpeg)
                        {
                            Utils.startTimeMeasure("Bitmap_decompress");

                            if (curUpdateTargetComoonentOrBuf == BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_1)
                            {
                                skiaBufStreams[0].Write(compressedStream.ToArray(), 0, metaData.dataSize);
                            }
                            else
                            {
                                skiaBufStreams[1].Write(compressedStream.ToArray(), 0, metaData.dataSize);
                            }

                            Console.WriteLine("elapsed for jpeg decompress: " + Utils.stopMeasureAndGetElapsedMilliSeconds("Bitmap_decompress").ToString() + " msec");
                        }
                        else if (metaData.compressed)
                        {
                            try
                            {
                                Utils.startTimeMeasure("Bitmap_decompress");
                                compressedStream.Position = 0;
                                using (var gzip = new GZipStream(compressedStream, CompressionMode.Decompress, true))
                                {
                                    var tmpDecompedStream = new MemoryStream();
                                    gzip.CopyTo(tmpDecompedStream);
                                    if (curUpdateTargetComoonentOrBuf == BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_1)
                                    {
                                        skiaBufStreams[0].Write(tmpDecompedStream.ToArray(), 0, metaData.imageDataSize);
                                    }
                                    else
                                    {
                                        skiaBufStreams[1].Write(tmpDecompedStream.ToArray(), 0, metaData.imageDataSize);
                                    }

                                    Console.WriteLine("elapsed for bitmap decompress: " + Utils.stopMeasureAndGetElapsedMilliSeconds("Bitmap_decompress").ToString() + " msec");
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }

                        // このメソッドの中でImageコンポーネントへの更新通知も行う
                        dataUpdateTargetImageComponentToggle();

                        Console.WriteLine("new capture image received and update bitmap display!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        //tcs.SetException(ex);
                    }

                    if (isBitDisplayComponetsAdded)
                    {
                        totalDisplayedFrames += 1;
                    }

                }
            });
            //var task = tcs.Task;
            //try
            //{
            //    task.Wait();
            //}
            //catch { }
            Console.WriteLine("image update Invoked at EndDataRecievedCallback!");
        }


        private void Socket_DataRecievedCallback(byte[] data, int dataSize, int offset /* do not use this value */)
        {
            //var tcs = new TaskCompletionSource<bool>();
            byte[] local_buf = new byte[dataSize];
            Array.Copy(data, 0, local_buf, 0, dataSize);
            Task.Run(() =>
            {
                lock (this)
                {
                    try
                    {
                        if (metaData.compressed || GlobalConfiguration.isConvJpeg || GlobalConfiguration.isStreamRawH264Data)
                        {
                            compressedStream.Write(local_buf, 0, dataSize);
                        }
                        else
                        {
                            if (curUpdateTargetComoonentOrBuf == BITMAP_DISPLAY_COMPONENT_TAG.COMPONENT_1)
                            {
                                skiaBufStreams[0].Write(local_buf, 0, dataSize);
                            }
                            else
                            {
                                skiaBufStreams[1].Write(local_buf, 0, dataSize);
                            }

                        }
                        //tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        //tcs.SetException(ex);
                    }
                }
            });

            // wait until codes passed to Device.BeginInvokeOnMainThread func
            //var task = tcs.Task;
            //try
            //{
            //    task.Wait();
            //}
            //catch { }
        }

        private void Socket_ConnectionFailedCallback(string error)
        {
            Task.Run(() =>
            {
                socket.Dispose();
                socket = null;
            });
        }

        private void ApplySettings(MetaDataTypes type)
        {
            if (isDisposed || socket == null) return;

            var metaData = new MetaData()
            {
                type = type,
                //compressed = false,
                compressed = true,
                resolutionScale = .5f, //.3f,
                //screenIndex = 0,
                targetFPS = 1.0f,
                dataSize = -1
            };

            socket.SendMetaData(metaData);
        }

        private void Socket_ConnectedCallback()
        {
            ApplySettings(MetaDataTypes.StartCapture);
        }

        private void Socket_DisconnectedCallback()
        {
            DisposePageHavingResources();
        }

        //protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        //{
        //    skipImageUpdate = true;
        //    base.OnRenderSizeChanged(sizeInfo);
        //}

        //protected override void OnLocationChanged(EventArgs e)
        //{
        //    skipImageUpdate = true;
        //    base.OnLocationChanged(e);
        //}

        //private void Window_KeyDown(object sender, KeyEventArgs e)
        //{
        //    lock (this)
        //    {
        //        if (connectedToLocalPC || isDisposed || uiState != UIStates.Streaming || socket == null) return;

        //        byte specialKeyCode = 0, keycode = (byte)e.Key;

        //        // get special key
        //        if (Keyboard.IsKeyDown(Key.LeftShift)) specialKeyCode = (byte)Key.LeftShift;
        //        else if (Keyboard.IsKeyDown(Key.RightShift)) specialKeyCode = (byte)Key.RightShift;
        //        else if (Keyboard.IsKeyDown(Key.LeftCtrl)) specialKeyCode = (byte)Key.LeftCtrl;
        //        else if (Keyboard.IsKeyDown(Key.RightCtrl)) specialKeyCode = (byte)Key.RightCtrl;
        //        else if (Keyboard.IsKeyDown(Key.LeftAlt)) specialKeyCode = (byte)Key.LeftAlt;
        //        else if (Keyboard.IsKeyDown(Key.RightAlt)) specialKeyCode = (byte)Key.RightAlt;

        //        // make sure special key isn't the same as normal key
        //        if (specialKeyCode == keycode) specialKeyCode = 0;

        //        // send key event
        //        var metaData = new MetaData()
        //        {
        //            type = MetaDataTypes.UpdateKeyboard,
        //            keyCode = (byte)keycode,
        //            specialKeyCode = specialKeyCode,
        //            dataSize = -1
        //        };

        //        socket.SendMetaData(metaData);
        //        e.Handled = true;
        //    }
        //}

        //        private void Refresh()
        //        {
        //            networkDiscovery = new NetworkDiscovery(NetworkTypes.Client);
        //            var hosts = networkDiscovery.Find("SimpleRemoteDesktop");
        //            Dispatcher.InvokeAsync(delegate ()
        //            {
        //                foreach (var host in hosts)
        //                {
        //                    serverComboBox.Items.Add(host);
        //                }

        //                if (hosts.Count != 0) serverComboBox.SelectedIndex = 0;
        //                refreshingGrid.Visibility = Visibility.Hidden;
        //            });
        //        }
    }
}
