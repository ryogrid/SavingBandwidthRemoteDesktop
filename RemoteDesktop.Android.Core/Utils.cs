//using Plugin.ImageResizer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
//using DevKit.Xamarin.ImageKit;
//using DevKit.Xamarin.ImageKit.Abstractions;



namespace RemoteDesktop.Android.Core
{
    public class DoNothingStream : Stream
    {
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                return 0;
            }
        }

        public override long Position
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0;
        }

        public override void SetLength(long value)
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
        }
    }

    public class ZeroMemoryPipeStream : Stream
    {
        public delegate void ZeroMemoryWriteHandler(byte[] buffer, int offset, int count);
        public event ZeroMemoryWriteHandler writeHandler;
        private long allWroteBytes = 0;
        private int allWriteCount = 0; //回数

        public override bool CanRead
        {
            get
            {
                Console.WriteLine("call CanRead");
//                Console.Out.Flush();
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                Console.WriteLine("call CanSeek");
//                Console.Out.Flush();
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                Console.WriteLine("call CanWrite");
//                Console.Out.Flush();
                return true;
            }
        }

        public override long Length
        {
            get
            {
                Console.WriteLine("call Length. return value is {0}", allWroteBytes);
//                Console.Out.Flush();
                return allWroteBytes;
            }
        }

        public override long Position
        {
            get
            {
                Console.WriteLine("call Position. return value is {0}", allWroteBytes);
//                Console.Out.Flush();
                return allWroteBytes;
            }
            set
            {
                Console.WriteLine("call Position");
//                Console.Out.Flush();
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            Console.WriteLine("call Flush");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Console.WriteLine("call Read. offset = {0}, count = {1}", offset, count);
//            Console.Out.Flush();
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Console.WriteLine("call Seek. value = {0}", offset);
//            Console.Out.Flush();
            return 0;
        }

        public override void SetLength(long value)
        {
            Console.WriteLine("call SetLength. value = {0}", value);
///            Console.Out.Flush();
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            allWriteCount++;
            writeHandler(buffer, offset, count);
        }
    }

    public static class Utils
    {
        private static Dictionary<string, Stopwatch> sw_dic = new Dictionary<string, Stopwatch>();
        //private static Stopwatch sw = null;

        // [SuppressUnmanagedCodeSecurity]
        // [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall)]
        // public static extern void RtlZeroMemory(IntPtr dst, IntPtr length);
        //
        // [SuppressUnmanagedCodeSecurity]
        // [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        // public static extern IntPtr memset(IntPtr dest, int c, IntPtr count);

        public static void fillValueByteArray(byte[] buf, byte value, int offset)
        {
            int len = buf.Length;
            for (int ii = 0; ii < len; ii++)
            {
                buf[offset + ii] = value;
            }
        }

        public static void startTimeMeasure(string sw_name)
        {
            Stopwatch sw = null;
            try
            {
                sw = sw_dic[sw_name];
                sw.Reset();
            }
            catch (KeyNotFoundException ex)
            {
                sw = new Stopwatch();
                sw_dic[sw_name] = sw;
                Console.WriteLine(ex);
            }

            sw.Start();
        }

        public static long stopMeasureAndGetElapsedMilliSeconds(string sw_name)
        {
            Stopwatch sw = null;
            try
            {
                sw = sw_dic[sw_name];
            }
            catch (KeyNotFoundException ex)
            {
                //throw new Exception("specified Stopwatch not found!");
                Console.WriteLine("ERROR: specified Stopwatch not found. But running keeps (DEBUG)");
                return -1;
            }
            sw.Stop();
            var ret = sw.ElapsedMilliseconds;
            return ret;
        }

        public static long getUnixTime()
        {
            var now = DateTime.UtcNow;
            long unixtime = (long)(now - new DateTime(1970, 1, 1)).TotalSeconds;
            return unixtime;
        }

        public static IPAddress getLocalIP()
        {
            String hostName = Dns.GetHostName();    // 自身のホスト名を取得
            IPAddress[] addresses = Dns.GetHostAddresses(hostName);
            foreach (IPAddress address in addresses)
            {
                // IPv4 のみ
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return address;
                    ///Console.WriteLine("getLocalIP func got IP Address: " + address.ToString());
                }
            }
            return null;

        }

        // 渡したバイト配列はバッファとして内包していない
        public static MemoryStream getAddHeaderdBitmapStreamByPixcels(byte[] pixels, int width, int height)
        {
            //buffer作成
            var numPixels = width * height;
            //var numPixelBytes = 2 * numPixels; // RGB565
            var numPixelBytes = 3 * numPixels; // RGB24
            //var numPixelBytes = 4 * numPixels; // RGB32
            var headerSize = 54;
            var filesize = headerSize + numPixelBytes;


            //bufferにheader情報を書き込む
            var memoryStream = new MemoryStream(filesize);
            var writer = new BinaryWriter(memoryStream, Encoding.UTF8);
            writer.Write(new char[] { 'B', 'M' });
            writer.Write(filesize);
            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write(headerSize);

            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            //writer.Write((short)16); //RGB565 = 16bit
            writer.Write((short)24); //RGB24
            //writer.Write((short)32); //RGB32
            writer.Write(0);
            writer.Write(numPixelBytes);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            writer.Write(pixels);

            writer.Flush();

            return memoryStream;
        }

        public static void saveByteArrayToFile(byte[] data, string path)
        {
            FileStream fs = new FileStream(path, FileMode.Create);
            fs.Write(data, 0, data.Length);
            fs.Flush();
            fs.Close();
        }

        public static byte[] readByteArrayFromFile(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            MemoryStream ms = new MemoryStream();
            byte[] buf = new byte[2048];
            int len = 0;
            while((len = fs.Read(buf, 0, buf.Length)) > 0){
                ms.Write(buf, 0, len);
            }
            byte[] ret_data = ms.ToArray();
            fs.Close();
            fs.Dispose();
            ms.Close();
            ms.Dispose();
            return ret_data;
        }

        public static void setStdoutOff()
        {
            DoNothingStream dns = new DoNothingStream();
            StreamWriter writer = new StreamWriter(dns);
            Console.SetOut(writer);
        }

        public static int CLIP(int x)
        {
            int ret = x;
            if (x < 0)
            {
                ret = 0;
            }
            else if (x > 255)
            {
                ret = 255;
            }
            return ret;
        }

        public static int CONVERT_R(int Y, int V)
        {
            return ((298 * (Y - 16) + 409 * (V - 128) + 128) >> 8);
        }

        public static int CONVERT_G(int Y, int U, int V)
        {
            return ((298 * (Y - 16) - 100 * (U - 128) - 208 * (V - 128) + 128) >> 8);
        }

        public static int CONVERT_B(int Y, int U)
        {
            return ((298 * (Y - 16) + 516 * (U - 128) + 128) >> 8);
        }

        public static byte[] NV12ToRGBA8888(byte[] yuvBuffer, int width, int height)
        {
            byte[] rgbBuffer = new byte[width * height * 4];
            byte[] y = new byte[2]{ 0, 0 };
            byte u = 0;
            byte v = 0;
            int r = 0;
            int g = 0;
            int b = 0;
            int uv_idx = width * height;
            for (int rowCnt = 0; rowCnt < height; rowCnt++)
            {
                for (int colCnt = 0; colCnt < width; colCnt += 2)
                {
                    u = yuvBuffer[uv_idx + colCnt + 0];
                    v = yuvBuffer[uv_idx + colCnt + 1];

                    for (int cnt = 0; cnt < 2; cnt++)
                    {
                        y[cnt] = yuvBuffer[rowCnt * width + colCnt + cnt];

                        r = CONVERT_R(y[cnt], v);
                        r = CLIP(r);
                        g = CONVERT_G(y[cnt], u, v);
                        g = CLIP(g);
                        b = CONVERT_B(y[cnt], u);
                        b = CLIP(b);
                        rgbBuffer[(rowCnt * width + colCnt + cnt) * 4 + 0] = (byte)r;
                        rgbBuffer[(rowCnt * width + colCnt + cnt) * 4 + 1] = (byte)g;
                        rgbBuffer[(rowCnt * width + colCnt + cnt) * 4 + 2] = (byte)b;
                        rgbBuffer[(rowCnt * width + colCnt + cnt) * 4 + 3] = (byte)0xFF;
                    }
                }

                uv_idx += width * (rowCnt % 2);
            }
            return rgbBuffer;
        }

        public static byte[] YV12ToRGBA8888(byte[] yuvBuffer, int width, int height)
        {
            byte[] rgbBuffer = new byte[width * height * 4];
            byte[] y = new byte[] { 0, 0 };
            byte u = 0;
            byte v = 0;
            int r = 0;
            int g = 0;
            int b = 0;
            int u_idx_start = width * height;
            int v_idx_start = width * height + ((width * height) / 4);
            for (int rowCnt = 0; rowCnt < height; rowCnt++)
            {
                for (int colCnt = 0; colCnt < width; colCnt+=2)
                {
                    u = yuvBuffer[u_idx_start + colCnt];
                    v = yuvBuffer[v_idx_start + colCnt];

                    for (int cnt = 0; cnt < 2; cnt++)
                    {
                        y[cnt] = yuvBuffer[rowCnt * width + colCnt + cnt];

                        r = CONVERT_R(y[cnt], v);
                        r = CLIP(r);
                        g = CONVERT_G(y[cnt], u, v);
                        g = CLIP(g);
                        b = CONVERT_B(y[cnt], u);
                        b = CLIP(b);
                        rgbBuffer[(rowCnt * width + colCnt) * 4 + 0] = (byte)r;
                        rgbBuffer[(rowCnt * width + colCnt) * 4 + 1] = (byte)g;
                        rgbBuffer[(rowCnt * width + colCnt) * 4 + 2] = (byte)b;
                        rgbBuffer[(rowCnt * width + colCnt) * 4 + 3] = (byte)0xFF;
                    }
                }

                u_idx_start += (width / 2) * (rowCnt % 2);
                v_idx_start += (width / 2) * (rowCnt % 2);
            }
            return rgbBuffer;
        }

        public static byte[] YV12ToRGBA8888_2(byte[] yuvBuffer, int width, int height)
        {
            byte[] rgbBuffer = new byte[width * height * 4];
            //sbyte[] yuvBufferSbyte = convertByteArrToSbyteArr(yuvBuffer);
            int[] y = new int[] { 0, 0 };
            int u = 0;
            int v = 0;
            int r = 0;
            int g = 0;
            int b = 0;
            int u_idx_start = width * height;
            int v_idx_start = width * height + ((width * height) / 4);
            for (int rowCnt = 0; rowCnt < height; rowCnt++)
            {
                for (int colCnt = 0; colCnt < width; colCnt+=2)
                {
                    u = yuvBuffer[u_idx_start + colCnt];
                    v = yuvBuffer[v_idx_start + colCnt];

                    for (int cnt = 0; cnt < 2; cnt++)
                    {
                        y[cnt] = yuvBuffer[rowCnt * width + colCnt + cnt];

                        r = CONVERT_R(y[cnt], v);
                        r = CLIP(r);
                        g = CONVERT_G(y[cnt], u, v);
                        g = CLIP(g);
                        b = CONVERT_B(y[cnt], u);
                        b = CLIP(b);
                        rgbBuffer[(rowCnt * width + colCnt) * 4 + 0] = (byte)r;
                        rgbBuffer[(rowCnt * width + colCnt) * 4 + 1] = (byte)g;
                        rgbBuffer[(rowCnt * width + colCnt) * 4 + 2] = (byte)b;
                        rgbBuffer[(rowCnt * width + colCnt) * 4 + 3] = (byte)0xFF;
                    }
                }

                u_idx_start += (width / 2) * (rowCnt % 2);
                v_idx_start += (width / 2) * (rowCnt % 2);
            }
            return rgbBuffer;
        }

/*
        public static byte RYUVRGB24CI2V16_RANGECHECK_0TO255(int x) {
            return (byte)((((x) <= 255) && ((x) >= 0)) ? ((x)) : (((x) > 255) ? (255) : (0)));
         }

        public static double RYUVRGB24CI2V16_U0 =  2.032;
        public static double RYUVRGB24CI2V16_U1	=	-0.394;
        public static double RYUVRGB24CI2V16_V1	=	-0.581;
        public static double RYUVRGB24CI2V16_V0	=	 1.140;

        public static byte[] YV12ToRGBA8888_Fast(byte[] yuvBuffer, int width, int height){
          byte[] rgbBuffer = new byte[width * height * 4];
          sbyte[] yuv_sbyte_all = convertByteArrToSbyteArr(yuvBuffer);
          sbyte[]	py = new sbyte[width*height];
          sbyte[]	pu = new sbyte[(int)(width*height * (1/4.0))];
          sbyte[]	pv = new sbyte[(int)(width*height * (1/4.0))];

          Array.Copy(yuv_sbyte_all, 0, py, 0, py.Length);
          Array.Copy(yuv_sbyte_all, py.Length, pu, 0, pu.Length);
          Array.Copy(yuv_sbyte_all, py.Length + pu.Length, pv, 0, pv.Length);
          int		lumX	= width;
          int		lumY	= height;
          int		uvX		= width >> 1;

          int x,y;
          int lumposy;
          int rgbposy;
          int uvposy;
          int r, b, g;
          byte a;

//          int tworows  = lumX << 1;
          int tworows  = lumX << 1;
          int rgb1row  = (width * 4);
          int rgb2rows = (width * 4) << 1;

          for(y = 0,lumposy = 0,uvposy = 0,rgbposy = 0;	y < lumY;	y += 2,lumposy += tworows,uvposy += uvX,rgbposy += rgb2rows)
          {
            int lumpos0 = lumposy;					///< Reset to start of rows.
            int lumpos1 = lumposy + lumX;
            int uvpos	 = uvposy;

            int rgbpos0 = rgbposy;
            int rgbpos1 = rgbposy + rgb1row;

            for(x = 0; x < lumX; x += 2)
            {
              double lum00 = (double)((int)(py[lumpos0++]));
              double u		 = (double)(((int)(pu[uvpos])) - 128);
              double v		 = (double)(((int)(pv[uvpos++])) - 128);

              /// Lum00, u and v.

              /// Fast calculation intermediate variables.
              double cc = (RYUVRGB24CI2V16_U0 * u) + 0.5;
              double cb = (RYUVRGB24CI2V16_U1 * u) + (RYUVRGB24CI2V16_V1 * v) + 0.5;
              double ca = (RYUVRGB24CI2V16_V0 * v) + 0.5;

              r = (int)(lum00 + ca);
              g = (int)(lum00 + cb);
              b = (int)(lum00 + cc);
              a = 0xFF;

              /// R, G , B have range 0..255.
              rgbBuffer[rgbpos0++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(r);
              rgbBuffer[rgbpos0++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(g);
              rgbBuffer[rgbpos0++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(b);
              rgbBuffer[rgbpos0++] = a;

              /// Lum01.
              double lum01 = (double)((int)(py[lumpos0++]));

              r = (int)(lum01 + ca);
              g = (int)(lum01 + cb);
              b = (int)(lum01 + cc);

              rgbBuffer[rgbpos0++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(r);
              rgbBuffer[rgbpos0++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(g);
              rgbBuffer[rgbpos0++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(b);
              rgbBuffer[rgbpos0++] = a;

              /// Lum10.
              double lum10 = (double)((int)(py[lumpos1++]));

              r = (int)(lum10 + ca);
              g = (int)(lum10 + cb);
              b = (int)(lum10 + cc);

              rgbBuffer[rgbpos1++] =  RYUVRGB24CI2V16_RANGECHECK_0TO255(r);
              rgbBuffer[rgbpos1++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(g);
              rgbBuffer[rgbpos1++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(b);
              rgbBuffer[rgbpos1++] = a;

              /// Lum11.
              double lum11 = (double)((int)(py[lumpos1++]));

              r = (int)(lum11 + ca);
              g = (int)(lum11 + cb);
              b = (int)(lum11 + cc);

              rgbBuffer[rgbpos1++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(r);
              rgbBuffer[rgbpos1++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(g);
              rgbBuffer[rgbpos1++] = RYUVRGB24CI2V16_RANGECHECK_0TO255(b);
              rgbBuffer[rgbpos1++] = a;

            }//end for x...

          }//end for y...

            return rgbBuffer;
        }
*/

        //// for canvas setting is Argb8888
        public static byte[] convertBitmapBGR24toBGRA32(byte[] bitmap)
        {
            int pixels = bitmap.Length / 3;
            byte[] conved = new byte[pixels * 4];
            for (int idx = 0; idx<pixels; idx++)
            {
                conved[idx * 4] = bitmap[idx * 3];
                conved[idx * 4 + 1] = bitmap[idx * 3 + 1];
                conved[idx * 4 + 2] = bitmap[idx * 3 + 2];
                conved[idx * 4 + 3] = 0xFF;
            }

            return conved;
        }

        public static String getFormatedCurrentTime()
        {
            return DateTime.Now.ToString("yyyy / MM / dd hh: mm: ss.fff");
        }

        public static byte[] convertCharArrayToByteArray(char[] arr)
        {
            MemoryStream ms = new MemoryStream();
            foreach(char ch in arr)
            {
                ms.Write(BitConverter.GetBytes(ch), 0, 2);
            }
            return ms.ToArray();
        }

        // unsigned 16 bit PCM のバイト配列 を 符号付き16bit整数に変換する
        public static short[] convertBytesToShortArr(byte[] data)
        {
            short[] ret_data = new short[data.Length / 2];
            for(int ii = 0, rslt_idx = 0; ii < data.Length; ii+=2, rslt_idx++)
            {
                ret_data[rslt_idx] = BitConverter.ToInt16(data, ii);
            }

            return ret_data;
        }

        public static byte[] convertShortArrToBytes(short[] data)
        {
            MemoryStream ms = new MemoryStream();
            foreach(short sht in data)
            {
                ms.Write(BitConverter.GetBytes(sht), 0, 2);
            }
            return ms.ToArray();
        }

        public static sbyte[] convertByteArrToSbyteArr(byte[] data){
            //sbyte[] conved = ((data as Array) as sbyte[]);
            sbyte[] conved = new sbyte[data.Length];
            Buffer.BlockCopy(data, 0, conved, 0, data.Length);
            return conved;
        }
    }

    public static class EndianReverser
    {
        // .NETはintは32bitという風にサイズが固定で変化しない

        // 共通化できるものは処理を移譲する
        public static char   Reverse(char value)   => (char)Reverse((ushort)value);
        public static short  Reverse(short value)  => (short)Reverse((ushort)value);
        public static int    Reverse(int value)    => (int)Reverse((uint)value);
        public static long   Reverse(long value)   => (long)Reverse((ulong)value);

        public static void uint16_bytes_reverse(byte[] data)
        {
            ushort val = 0;
            byte[] conved_data;
            for(int ii = 0; ii < data.Length; ii += 2)
            {
                val = BitConverter.ToUInt16(data, ii);
                conved_data = BitConverter.GetBytes(Reverse(val));
                data[ii] = conved_data[0];
                data[ii + 1] = conved_data[1];
            }
        }

        // 伝統的な16ビット入れ替え処理16bit
        public static ushort Reverse(ushort value)
        {
            return (ushort)((value & 0xFF) << 8 | (value >> 8) & 0xFF);
        }

        // 伝統的な16ビット入れ替え処理32bit
        public static uint Reverse(uint value)
        {
            return (value & 0xFF) << 24 |
                    ((value >> 8) & 0xFF) << 16 |
                    ((value >> 16) & 0xFF) << 8 |
                    ((value >> 24) & 0xFF);
        }

        // 伝統的な16ビット入れ替え処理64bit
        public static ulong Reverse(ulong value)
        {
            return (value & 0xFF) << 56 |
                    ((value >>  8) & 0xFF) << 48 |
                    ((value >> 16) & 0xFF) << 40 |
                    ((value >> 24) & 0xFF) << 32 |
                    ((value >> 32) & 0xFF) << 24 |
                    ((value >> 40) & 0xFF) << 16 |
                    ((value >> 48) & 0xFF) << 8 |
                    ((value >> 56) & 0xFF);
        }

        // 浮動小数点はちょっと効率悪いけどライブラリでできる操作でカバーする
        public static float Reverse(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value); // これ以上いい処理が思いつかない
            Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        public static double Reverse(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }
    }

}
