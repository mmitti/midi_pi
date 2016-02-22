using MIDI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;

namespace App1
{
    public delegate void OnBarBeatChangeEvent(uint bar, byte beat);
    public delegate void OnTempoChangeEvent(uint tempo);
    public delegate void OnLEDEvent(byte id);
    public class SMFPlayer
    {
        SMF mSMF;
        Task mPlayTask;
        private uint mBar;
        private byte mBeat;
        private ulong mTotalTick;
        private uint mTick;
        public uint Bar { get { return mBar; } }
        public ulong TotalTick { get { return mTotalTick; } }
        public byte Beat { get { return mBeat; } }
        public uint Tick { get { return mTick; } }
        CancellationTokenSource mTaskCancelToken;
        IMidiOutPort mMidi;
        public event OnBarBeatChangeEvent OnBarBeatChanged;
        public event OnTempoChangeEvent OnTempoChanged;
        public event OnLEDEvent OnLED;
        public SMFPlayer(SMF smf, IMidiOutPort p)
        {
            mSMF = smf;
            mMidi = p;

        }
        public void Play()
        {
            mTaskCancelToken = new CancellationTokenSource();
            mPlayTask = Task.Run(() => {
                _PlayTask(mTaskCancelToken.Token);
            }, mTaskCancelToken.Token);
        }
        public void Stop()
        {
            if (mTaskCancelToken != null) mTaskCancelToken.Cancel();
        }

        private async void _PlayTask(CancellationToken t)
        {
            int event_count = 0;
            int message_count = 0;
            uint beat = 0;
            uint tick = 0;
            ulong difftime = 0;
            long last_ms = 0;
            uint tickperbeat = mSMF.TimeMode;

            Meter meter = new MeterEvent(0, 0, 4, 2, 0x18, 8);//4/4
            Key key = new KeyEvent(0, 0, 0, 0);
            Tempo tempo = new TempoEvent(0, 0, 0x07,0xA1,0x20);//120
            

            Stopwatch s = new Stopwatch();
            ulong tickus = (ulong)(tempo.TempoUS / tickperbeat);
            s.Start();
            if (OnBarBeatChanged != null) OnBarBeatChanged(0, 0);
            while (true)
            {
                long em = s.ElapsedMilliseconds;
                difftime += (ulong)(em-last_ms) * 1000;
                last_ms = em;
                if (difftime > tickus)
                {
                    tick += (uint)(difftime / tickus);
                    mTotalTick += (uint)(difftime / tickus);
                    difftime = difftime % tickus;
                    if (tick >= tickperbeat)
                    {
                        beat += (uint)(tick / tickperbeat);
                        tick = (uint)(tick % tickperbeat);
                        if (OnBarBeatChanged != null) OnBarBeatChanged(beat / 4, (byte)(beat % 4));
                        mBar = (uint)(beat / 4);
                        mBeat = (byte)(beat % 4 + 1);
                        
                    }
                    mTick = tick;
                }
                while(message_count < mSMF.MidiMessages.Count && 
                    mSMF.MidiMessages[message_count].Beat * tickperbeat + mSMF.MidiMessages[message_count].Tick < beat*tickperbeat + tick)
                {
                    mMidi.SendMessage(mSMF.MidiMessages[message_count].Message);
                    message_count++;
                }
                while(event_count < mSMF.SystemEvents.Count &&
                    mSMF.SystemEvents[event_count].Beat * tickperbeat + mSMF.SystemEvents[event_count].Tick < beat * tickperbeat + tick)
                {
                    var ev = mSMF.SystemEvents[event_count];
                    if (ev is MeterEvent) meter = (MeterEvent)ev;
                    else if (ev is TempoEvent) {
                        tempo = (TempoEvent)ev;
                        tickus = (ulong)(tempo.TempoUS / tickperbeat);
                        if (OnTempoChanged != null) OnTempoChanged((uint)Math.Round((double)60 * 1000 * 1000 / tempo.TempoUS));
                    }
                    else if (ev is KeyEvent) key = (KeyEvent)ev;
                    else if(ev is LEDEvent)
                    {
                        if (OnLED != null) OnLED(((LEDEvent)ev).LED);
                    } 
                    event_count++;
                }
                    if (t.IsCancellationRequested)
                {
                    
                    //終了
                    break;
                }
            }
        }


    }

}
