using RemoteDesktop.Android.Core;
using ScnViewGestures.Plugin.Forms;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace RemoteDesktop.Client.Android
{
    class InputManager
    {
        private DataSocket socket;
        private Xamarin.Forms.AbsoluteLayout layout;
        private ViewGestures tapViewGestures;
        private RDPSessionPage rdpSessionPage;
        private int internalCursorPosAppCanvasX = -1;
        private int internalCursorPosAppCanvasY = -1;

        public InputManager(DataSocket socket, Xamarin.Forms.AbsoluteLayout layout, RDPSessionPage rdpSessionPage)
        {
            this.socket = socket;
            this.layout = layout;
            this.rdpSessionPage = rdpSessionPage;

			//var pressLabel = new Label
			//{
			//	Text = "Tap me",
			//	FontSize = 30
			//};

            tapViewGestures = new ViewGestures
            {
                BackgroundColor = Color.Transparent,
                //BackgroundColor = Color.MistyRose,
                //Content = pressLabel,
                //AnimationEffect = ViewGestures.AnimationType.atScaling,
                //AnimationScale = -5,
                AnimationEffect = ViewGestures.AnimationType.atNone,
//                HorizontalOptions = LayoutOptions.FillAndExpand,
//                VerticalOptions = LayoutOptions.FillAndExpand
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
			};
            //tapViewGestures.Tap += (s, e) => DisplayAlert("Tap", "Gesture finished", "OK");
            tapViewGestures.SwipeRight += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() => {
                    Console.WriteLine("Swipe!");

                    inputUpdate(1, -1, -1); //UP
                });
            };
            tapViewGestures.SwipeLeft += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() => {
                    Console.WriteLine("Swipe!");

                    inputUpdate(2, -1, -1); //DOWN
                });
            };
            tapViewGestures.SwipeUp += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() => {
                    Console.WriteLine("Swipe!");

                    inputUpdate(3, -1, -1); //LEFT
                });
            };
            tapViewGestures.SwipeDown += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() => {
                    Console.WriteLine("Swipe!");

                    inputUpdate(4, -1, -1); //RIGHT
                });
            };
            tapViewGestures.Tap += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() => {
                    Console.WriteLine("Tap!");

                    inputUpdate(5, -1, -1); // left click
                });
            };
            tapViewGestures.LongTap += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() => {
                    Console.WriteLine("Long Tap!");

                    inputUpdate(6, -1, -1); // right click
                });
            };
            tapViewGestures.Drag += (s, e) =>
            {

                DragEventArgs moved = (DragEventArgs)e;
/*
                Device.BeginInvokeOnMainThread(() =>
                {
                    rdpSessionPage.DisplayAlert("", moved.DistanceX.ToString() + "," + moved.DistanceY.ToString(), "OK");
                });
*/

                Device.BeginInvokeOnMainThread(() => {
                    Console.WriteLine("Drag!");

                    inputUpdate(7, (int)moved.DistanceX, (int)moved.DistanceY); // Drag
                });

            };
            //layout.Children.Add(tapViewGestures, new Rectangle(0, 0, RDPSessionPage.width, RDPSessionPage.height));
        }

        // called from UI thread
        public void addGestureViewLayer()
        {
            Device.BeginInvokeOnMainThread(() => {
                layout.Children.Add(tapViewGestures, new Rectangle(0, 0, RDPSessionPage.width, RDPSessionPage.height));
            });
        }
//        private void inputUpdate(object state)
        private void inputUpdate(int code, int x, int y)
        {
            lock (this)
            {
                //if (!mouseUpdate) return;
                //mouseUpdate = false;

                //if (connectedToLocalPC || isDisposed || uiState != UIStates.Streaming || socket == null || bitmap == null) return;

                if(rdpSessionPage.skiaCanvasWidth == -1)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        rdpSessionPage.canvas.InvalidateSurface();
                    });
                    // skia canvas のサイズを得てから処理したいので、returnする
                    return;
                }
                Console.WriteLine("inputUpdate: skiaCanvasWidth is OK. so start prrocessing input information.");

                if(internalCursorPosAppCanvasX == -1)
                {
                    internalCursorPosAppCanvasX = rdpSessionPage.skiaCanvasWidth / 2;
                    internalCursorPosAppCanvasY = rdpSessionPage.skiaCanvasHeight / 2;
                }

                if(code == 7) // drag
                {
                    internalCursorPosAppCanvasX += x;
                    internalCursorPosAppCanvasY += y;
                    if (internalCursorPosAppCanvasX < 0) internalCursorPosAppCanvasX = 0;
                    if (internalCursorPosAppCanvasX > rdpSessionPage.skiaCanvasWidth) internalCursorPosAppCanvasX = rdpSessionPage.skiaCanvasWidth;
                    if (internalCursorPosAppCanvasY < 0) internalCursorPosAppCanvasY = 0;
                    if (internalCursorPosAppCanvasY > rdpSessionPage.skiaCanvasHeight) internalCursorPosAppCanvasX = rdpSessionPage.skiaCanvasHeight;
/*
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        rdpSessionPage.DisplayAlert("", internalCursorPosAppCanvasX.ToString() + "," + internalCursorPosAppCanvasY.ToString(), "OK");
                    });]
*/
                    return;
                }

                var task = Task.Run(() =>
                {
                    //if (isDisposed || uiState != UIStates.Streaming || socket == null || bitmap == null) return;

                    if (socket.IsConnected() == false) return;

                    var metaData = new MetaData()
                    {
                        type = MetaDataTypes.UpdateMouse,
                        /*
                                                mouseX = (short)((mousePoint.X / image.ActualWidth) * this.metaData.screenWidth),
                                                mouseY = (short)((mousePoint.Y / image.ActualHeight) * this.metaData.screenHeight),
                                                mouseScroll = mouseScroll,
                                                mouseButtonPressed = inputMouseButtonPressed,
                        */
                        mouseButtonPressed = (byte)code,
                        dataSize = -1
                    };

                    socket.SendMetaData(metaData);
                });

                //if (mouseScrollCount == 0) mouseScroll = 0;
                //else --mouseScrollCount;
            }
        }

        public void setCursorPosFromServer(int x, int y)
        {
            internalCursorPosAppCanvasX = x;
            internalCursorPosAppCanvasY = y;
        }

        public int[] getCursorInternalCursorPos()
        {
            if (internalCursorPosAppCanvasX == -1)
            {
                return null;
            }
            int[] ret = new int[2];
            ret[0] = internalCursorPosAppCanvasX;
            ret[1] = internalCursorPosAppCanvasY;
            return ret;
        }
    }
}
