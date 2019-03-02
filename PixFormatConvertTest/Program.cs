using System;
using RemoteDesktop.Android.Core;

namespace PixFormatConvertTest
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] yuv_arr = Utils.readByteArrayFromFile("/Users/ryo/work/orylab/devenv/RemoteDesktopOneWindowForNovelGrame/scr_capture_yum420planner_yv12_1363x765.raw");
            byte[] rgba8888_arr = Utils.YV12ToRGBA8888(yuv_arr, 1363, 765);
            Utils.saveByteArrayToFile(rgba8888_arr, "/Users/ryo/work/orylab/devenv/RemoteDesktopOneWindowForNovelGrame/scr_capture_rgba8888_1363x765.raw");
            Console.WriteLine("convert finish!");
        }
    }
}
