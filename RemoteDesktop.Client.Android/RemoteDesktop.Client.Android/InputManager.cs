﻿using RemoteDesktop.Android.Core;
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
        public InputManager(DataSocket socket, Xamarin.Forms.AbsoluteLayout layout)
        {
            this.socket = socket;
            this.layout = layout;

            var tapViewGestures = new ViewGestures
			{
				BackgroundColor = Color.Transparent,
				//Content = pressLabel,
				AnimationEffect = ViewGestures.AnimationType.atScaling,
				AnimationScale = -5,
				HorizontalOptions = LayoutOptions.Center
			};
            //tapViewGestures.Tap += (s, e) => DisplayAlert("Tap", "Gesture finished", "OK");
            tapViewGestures.SwipeLeft += (s, e) =>
            {
                Device.BeginInvokeOnMainThread(() => { inputUpdate(null); });
            };

            layout.Children.Add(tapViewGestures);
        }

        private void inputUpdate(object state)
        {

            lock (this)
            {
                //if (!mouseUpdate) return;
                //mouseUpdate = false;

                //if (connectedToLocalPC || isDisposed || uiState != UIStates.Streaming || socket == null || bitmap == null) return;

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
                        mouseButtonPressed = 1,
                        dataSize = -1
                    };

                    socket.SendMetaData(metaData);
                });

                //if (mouseScrollCount == 0) mouseScroll = 0;
                //else --mouseScrollCount;
            }
        }
    }
}
