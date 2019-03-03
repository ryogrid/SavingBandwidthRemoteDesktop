using System;
using RemoteDesktop.Android.Core;

namespace PixFormatConvertTest
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] yuv_arr = Utils.readByteArrayFromFile("/Users/ryo/work/orylab/devenv/RemoteDesktopOneWindowForNovelGrame/scr_capture_yum420planner_yv12_1363x765.raw");
            //byte[] yuv_arr = Utils.readByteArrayFromFile("/Users/ryo/work/orylab/devenv/RemoteDesktopOneWindowForNovelGrame/tulips_yvu420_inter_planar_qcif_real_yv12_poi_176x144.yuv");
            //byte[] rgba8888_arr = Utils.YV12ToRGBA8888(yuv_arr, 1363, 765);
            byte[] rgba8888_arr = Utils.YV12ToRGBA8888_3(yuv_arr, 1363, 765);
            //byte[] rgba8888_arr = Utils.YV12ToRGBA8888_Fast(yuv_arr, 1363, 765);
            //byte[] rgba8888_arr = Utils.YV12ToRGBA8888(yuv_arr, 765, 1363);
            //byte[] rgba8888_arr = Utils.YV12ToRGBA8888(yuv_arr, 176, 144);
            //byte[] rgba8888_arr = Utils.YV12ToRGBA8888_Fast(yuv_arr, 176, 144);
            Utils.saveByteArrayToFile(rgba8888_arr, "/Users/ryo/work/orylab/devenv/RemoteDesktopOneWindowForNovelGrame/scr_capture_rgba8888_1363x765.raw");
            //Utils.saveByteArrayToFile(rgba8888_arr, "/Users/ryo/work/orylab/devenv/RemoteDesktopOneWindowForNovelGrame/tulips_yvu420_inter_planar_qcif_real_yv12_poi_176x144_rgba8888.raw");

            Console.WriteLine("convert finish!");
        }
    }
}
