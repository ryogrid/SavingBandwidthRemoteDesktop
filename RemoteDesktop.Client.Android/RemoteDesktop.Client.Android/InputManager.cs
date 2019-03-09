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
        public bool isAddedGestureViewLayer = false;
        private int orgPosXDragStart = int.MaxValue;
        private int orgPosYDragStart = int.MaxValue;
        private long lastTapAtUnixTime = 0;
        public int renderedAreaWidth = -1;
        public int renderedAreaHeight = -1;

        public InputManager(DataSocket socket, Xamarin.Forms.AbsoluteLayout layout, RDPSessionPage rdpSessionPage)
        {
            this.socket = socket;
            this.layout = layout;
            this.rdpSessionPage = rdpSessionPage;

            tapViewGestures = new ViewGestures
            {
                BackgroundColor = Color.Transparent,
                AnimationEffect = ViewGestures.AnimationType.atNone,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };


/*
            tapViewGestures.SwipeRight += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    Console.WriteLine("Swipe!");

                    inputUpdate(MouseInteractionType.LEFT_DOUBLE_CLICK, -1, -1);
                });
            };

            tapViewGestures.SwipeLeft += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    Console.WriteLine("Swipe!");

                    inputUpdate(2, -1, -1); //DOWN
                });
            };
            tapViewGestures.SwipeUp += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    Console.WriteLine("Swipe!");

                    inputUpdate(3, -1, -1); //LEFT
                });
            };
            tapViewGestures.SwipeDown += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    Console.WriteLine("Swipe!");

                    inputUpdate(4, -1, -1); //RIGHT
                });
            };
*/
            tapViewGestures.Tap += (s, e) =>
            {
                long cur = Utils.getUnixTime();
                long diff = cur - lastTapAtUnixTime;
                lastTapAtUnixTime = cur;
                 Console.WriteLine("Tap_diff {0}", diff);
                if (diff <= 2) //短い間隔でタップが複数回行われていた場合
                {
                    Task.Run(() =>
                    {
                        Console.WriteLine("Double Tap!");

                        inputUpdate(MouseInteractionType.LEFT_DOUBLE_CLICK, -1, -1);
                    });
                }
                else
                {
                    Task.Run(() =>
                    {
                        Console.WriteLine("Tap!");

                        inputUpdate(MouseInteractionType.LEFT_CLICK, -1, -1); // left click
                    });
                }
            };
            tapViewGestures.LongTap += (s, e) =>
            {
                Task.Run(() =>
                {
                    Console.WriteLine("Long Tap!");

                    inputUpdate(MouseInteractionType.RIGHT_CLICK, -1, -1); // right click
                });
            };
            tapViewGestures.Drag += (s, e) =>
            {
                DragEventArgs moved = (DragEventArgs)e;
                Task.Run(() =>
                {
                    Console.WriteLine("Drag!");

                    inputUpdate(MouseInteractionType.POSITION_SET, (int)moved.DistanceX, (int)moved.DistanceY); // Drag
                });
            };

            tapViewGestures.TouchBegan += (s, e) =>
            {
                orgPosXDragStart = int.MaxValue;
                orgPosYDragStart = int.MaxValue;
            };
        }

        // called from UI thread
        public void addGestureViewLayer()
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                layout.Children.Add(tapViewGestures, new Rectangle(0, 0, RDPSessionPage.width, RDPSessionPage.height));
            });
        }

        private void inputUpdate(MouseInteractionType code, int x, int y)
        {
            lock (this)
            {
                //if (!mouseUpdate) return;
                //mouseUpdate = false;

                //if (connectedToLocalPC || isDisposed || uiState != UIStates.Streaming || socket == null || bitmap == null) return;

                if (rdpSessionPage.skiaCanvasWidth == -1)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        rdpSessionPage.canvas.InvalidateSurface();
                    });
                    // skia canvas のサイズを得てから処理したいので、returnする
                    return;
                }
                //Console.WriteLine("inputUpdate: skiaCanvasWidth is OK. so start prrocessing input information.");

                if (internalCursorPosAppCanvasX == -1)
                {
                    internalCursorPosAppCanvasX = rdpSessionPage.skiaCanvasWidth / 2;
                    internalCursorPosAppCanvasY = rdpSessionPage.skiaCanvasHeight / 2;
                }

                if (code == MouseInteractionType.POSITION_SET) // drag
                {
                    //lastDragDistanceX = x;
                    //lastDragDistanceY = y;

                    if (orgPosXDragStart == int.MaxValue)
                    {
                        orgPosXDragStart = internalCursorPosAppCanvasX;
                        orgPosYDragStart = internalCursorPosAppCanvasY;
                    }
                    internalCursorPosAppCanvasX = orgPosXDragStart + x;
                    internalCursorPosAppCanvasY = orgPosYDragStart - y; // 画像をフリップしているので方向が逆になる
                    if (internalCursorPosAppCanvasX < 0) internalCursorPosAppCanvasX = 0;
                    if (internalCursorPosAppCanvasX > rdpSessionPage.skiaCanvasWidth) internalCursorPosAppCanvasX = rdpSessionPage.skiaCanvasWidth;
                    if (internalCursorPosAppCanvasY < 0) internalCursorPosAppCanvasY = 0;
                    if (internalCursorPosAppCanvasY > rdpSessionPage.skiaCanvasHeight) internalCursorPosAppCanvasY = rdpSessionPage.skiaCanvasHeight;

                    Console.WriteLine("called inputUpdate: {0}x{1}; moveX={2}, moveY={3}; updated cursor {4}, {5}", rdpSessionPage.skiaCanvasWidth, rdpSessionPage.skiaCanvasHeight, x, y, internalCursorPosAppCanvasX, internalCursorPosAppCanvasY);
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        rdpSessionPage.canvas.InvalidateSurface();
                    });

                }

                var task = Task.Run(() =>
                {
                    if (socket.IsConnected() == false) return;

                    short[] pos_arr = convetCoodClientPanelToPCScreen(internalCursorPosAppCanvasX, internalCursorPosAppCanvasY);
                    Console.WriteLine("SendCursorPos x={0} y={1}", pos_arr[1], pos_arr[0]);
                    var metaData = new MetaData()
                    {
                        type = MetaDataTypes.UpdateMouse,
                        mouseX = pos_arr[1],  // スマホでは画像が90度回転されて表示されているので x と y を入れ替えて渡す
                        mouseY = pos_arr[0],
                        mouseInteractionType = code,
                        dataSize = -1
                    };

                    socket.SendMetaData(metaData);
                });
            }
        }

        private short[] convetCoodClientPanelToPCScreen(int x, int y)
        {
            double ratio = rdpSessionPage.pcScreenWidth / (double)rdpSessionPage.skiaCanvasHeight;
            short[] ret = new short[2];
            //ret[0] = (short) ((x + ((rdpSessionPage.skiaCanvasWidth - renderedAreaWidth) /2)) * ratio);
            // 半分にしない方が良さそうだったのでそうしてみる
            //ret[0] = (short) ((x - (rdpSessionPage.skiaCanvasWidth - renderedAreaWidth)) * ratio);
            ret[0] = (short) ((x * ratio);            
            ret[1] = (short) (y * ratio);
            return ret;
        }

        // TODO: 使うことになったら上記と同じ補正が必要
        private int[] convertPCScreenToClientPanel(short x, short y)
        {
            double ratio = rdpSessionPage.skiaCanvasHeight / (double)rdpSessionPage.pcScreenWidth;
            int[] ret = new int[2];
            ret[0] = (int) (x * ratio);
            ret[1] = (int) (y * ratio);
            return ret;
        }

        // PC画面における座標値を渡せば良い
        public void setCursorPCCoodFromServer(short x, short y)
        {
            int[] pos_arr = convertPCScreenToClientPanel(x, y);
            internalCursorPosAppCanvasX = pos_arr[0];
            internalCursorPosAppCanvasY = pos_arr[1];
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
