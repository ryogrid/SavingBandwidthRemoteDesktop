﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using RemoteDesktop.Server.XamaOK;
using RemoteDesktop.Android.Core;

namespace RemoteDesktop.Server.XamaOK
{

    public sealed class AudioOutputWriter : IDisposable
    {

        #region イベント

        public event EventHandler<WaveInEventArgs> DataAvailable;

        #endregion

        #region フィールド

        //private readonly string _FileName;

        private WasapiLoopbackCapture _WaveIn;
        private UDPSender usender;
        //private WaveFileWriter _WaveFileWriter;
        private MMDevice m_device;
        public bool IsRecording = false;
        private RTPConfiguration rtp_config;

        //private int m_CurrentRTPBufferPos = 0;
        //private int m_RTPPartsLength = 0;
        //private byte[] m_FilePayloadBuffer;
        //private byte[] m_PartByte;
        //private int m_RTPPPartsLength = 0;

        #endregion

        #region コンストラクタ

        // this call block until recieved client connection request
        public AudioOutputWriter(MMDevice device, RTPConfiguration config)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            rtp_config = config;
            m_device = device;
//            this._FileName = fileName;
            this._WaveIn = new WasapiLoopbackCapture(m_device);
            this._WaveIn.DataAvailable += this.WaveInOnDataAvailable;
            this._WaveIn.RecordingStopped += this.WaveInOnRecordingStopped;

            usender = new UDPSender(Utils.getLocalIP().ToString(), 10000, 10);
            //this._Stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write);
            //this._WaveFileWriter = new WaveFileWriter(usender, this._WaveIn.WaveFormat);

            // キャプチャしたサウンドを読み込んだ際のハンドラ
            DataAvailable += AudioOutputWriterOnDataAvailable;

            Start(); // start capture and send stream
        }

        #endregion

        #region プロパティ

        //public bool IsRecording
        //{
        //    get {
        //        return IsRecording;
        //    }

        //    //private set;
        //}

        #endregion

        #region メソッド

        private void updateRTPConfiguration()
        {
            //throw new Exception();
        }

        public void Start()
        {
            this.IsRecording = true;
            this._WaveIn.StartRecording();

        }

        //public void Stop()
        //{
        //    this.IsRecording = false;
        //    this._WaveIn.StopRecording();
        //}

        private void resetAllInstanseState()
        {
            this.DataAvailable -= this.AudioOutputWriterOnDataAvailable;
            //this._WaveFileWriter.Close();
            //this._WaveFileWriter.Dispose();
            //this._WaveFileWriter = null;

            this._WaveIn.DataAvailable -= this.WaveInOnDataAvailable;
            this._WaveIn.RecordingStopped -= this.WaveInOnRecordingStopped;
            this._WaveIn.StopRecording();
            this._WaveIn.Dispose();
            this._WaveIn = null;

            //------

            this._WaveIn = new WasapiLoopbackCapture(m_device);
            this._WaveIn.DataAvailable += this.WaveInOnDataAvailable;
            this._WaveIn.RecordingStopped += this.WaveInOnRecordingStopped;

            //usender = new UDPSender(Utils.getLocalIP().ToString(), 10000, 10);
            //this._Stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write);
            //this._WaveFileWriter = new WaveFileWriter(usender, this._WaveIn.WaveFormat);

            // キャプチャしたサウンドを読み込んだ際のハンドラ
            this.DataAvailable += this.AudioOutputWriterOnDataAvailable;

            Start(); // start capture and send stream
        }

        #region オーバーライド
        #endregion

        #region イベントハンドラ

        private void AudioOutputWriterOnDataAvailable(object sender, WaveInEventArgs waveInEventArgs)
        {
            byte[] recorded_buf = waveInEventArgs.Buffer;
            int recorded_length = waveInEventArgs.BytesRecorded;

            Console.WriteLine($"{DateTime.Now:yyyy/MM/dd hh:mm:ss.fff} : {waveInEventArgs.BytesRecorded} bytes");
            try
            {
                if (rtp_config.isAlreadySetInfoFromSndCard == false)
                {
                    // キャプチャした音声データについて情報を設定
                    updateRTPConfiguration();
                    //m_RTPPartsLength = SoundUtils.GetBytesPerInterval((uint)rtp_config.SamplesPerSecond, rtp_config.SamplesPerSecond, rtp_config.Channels);
                    rtp_config.isAlreadySetInfoFromSndCard = true;
                }

                //次のコネクションが来ていないかチェックする
                usender.checkNextClient();
                if (usender.disconnected == true) // 次のコネクションが来ていたら (前の接続は切れている想定)
                {
                    //resetFileStreamingState();
                    resetAllInstanseState();
                    usender.disconnected = false;
                }

                //Wenn noch aktiv
                if (!usender.disconnected)
                {
                    //if ((m_CurrentRTPBufferPos + rtp_config.PacketSize) <= m_FilePayloadBuffer.Length)
                    //{
                    //    //Bytes senden
                    //    Array.Copy(m_FilePayloadBuffer, m_CurrentRTPBufferPos, m_PartByte, 0, m_RTPPartsLength);
                    //    m_CurrentRTPBufferPos += m_RTPPartsLength;
                    //    RTPPacket rtp = SoundUtils.ToRTPPacket(m_PartByte, rtp_config);
                    //    usender.SendBytes(rtp.ToBytes());
                    //}
                    //else
                    //{
                    //    //Rest-Bytes senden
                    //    int rest = m_FilePayloadBuffer.Length - m_CurrentRTPBufferPos;
                    //    Byte[] restBytes = new Byte[m_PartByte.Length];
                    //    Array.Copy(m_FilePayloadBuffer, m_CurrentRTPBufferPos, restBytes, 0, rest);
                    //    RTPPacket rtp = SoundUtils.ToRTPPacket(restBytes, rtp_config);
                    //    usender.SendBytes(rtp.ToBytes());

                    //    //if (m_Loop == false)
                    //    //{
                    //    //    //QueueTimer beenden
                    //    //    StopTimerStream();
                    //    //}
                    //    //else
                    //    //{
                    //    //    //Von vorne beginnen
                    //    //    m_CurrentRTPBufferPos = 0;
                    //    //}
                    //}

                    ////Wenn JitterBuffer
                    //if (rtp_config.JitterBuffer > 1)
                    //{
                    //Sounddaten in kleinere Einzelteile zerlegen
                    int bytesPerInterval = SoundUtils.GetBytesPerInterval((uint)rtp_config.SamplesPerSecond, rtp_config.BitsPerSample, rtp_config.Channels);
                    int count = recorded_length / bytesPerInterval;
                    int currentPos = 0;
                    Byte[] partBytes = new Byte[bytesPerInterval];
                    for (int i = 0; i < count; i++)
                    {
                        //Teilstück in RTP Packet umwandeln

                        Array.Copy(recorded_buf, currentPos, partBytes, 0, bytesPerInterval);
                        currentPos += bytesPerInterval;
                        RTPPacket rtp = SoundUtils.ToRTPPacket(partBytes, rtp_config);
                        usender.SendBytes(rtp.ToBytes());
                        //In Buffer legen
                        //m_JitterBuffer.AddData(rtp);
                    }
                    //    }
                    //    else
                    //    {
                    //        Byte[] justRecordedBuf = new byte[recorded_length];
                    //        Array.Copy(recorded_buf, 0, justRecordedBuf, 0, recorded_length);
                    //        //Alles in RTP Packet umwandeln
                    //        Byte[] rtp = SoundUtils.ToRTPData(justRecordedBuf, rtp_config);
                    //        //Absenden
                    //        usender.SendBytes(rtp);
                    //    }
                    //}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                resetAllInstanseState();
                //StopTimerStream();
            }
        }

        private void WaveInOnRecordingStopped(object sender, StoppedEventArgs e)
        {
            //if (this._WaveFileWriter != null)
            //{
            //    this._WaveFileWriter.Close();
            //    this._WaveFileWriter = null;
            //}

            //if (this.usender != null)
            //{
            //    this.usender.Close();
            //    this.usender = null;
            //}

            //this.Dispose();
        }

        private void WaveInOnDataAvailable(object sender, WaveInEventArgs e)
        {
            //this._WaveFileWriter.Write(e.Buffer, 0, e.BytesRecorded);
            this.DataAvailable?.Invoke(this, e);
        }

        #endregion

        #region ヘルパーメソッド
        #endregion

        #endregion

        #region IDisposable メンバー

        public void Dispose()
        {

            this.DataAvailable -= this.AudioOutputWriterOnDataAvailable;

            if(this._WaveIn != null)
            {
                this._WaveIn.StopRecording();
                this._WaveIn.DataAvailable -= this.WaveInOnDataAvailable;
                this._WaveIn.RecordingStopped -= this.WaveInOnRecordingStopped;
                this._WaveIn.Dispose();
            }

            //this._WaveFileWriter?.Dispose();
            this.usender?.Dispose();
        }

        #endregion

    }

}
