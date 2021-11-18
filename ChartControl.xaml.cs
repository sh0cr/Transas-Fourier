﻿using OxyPlot;
using OxyPlot.Series;
using OxyPlot.SkiaSharp.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using OxyPlot.SkiaSharp;
using SkiaSharp;
using System.Windows.Threading;

namespace FourierTransas
{
    public partial class ChartControl : UserControl
    {
        private PlotView[] plots;
        private DispatcherTimer _dTimer;
        //private CalculationService _service;

        [DllImport("Kernel32.dll")]
        public static extern uint GetCurrentThreadId();
        [DllImport("Kernel32.dll")]
        private static extern IntPtr GetCurrentThread();
        
        /// <summary>
        /// эмулирует построение и обновление графика сигнала
        /// </summary>
        public ChartControl()
        {
            InitializeComponent();
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            SkiaRenderContext rc = new SkiaRenderContext() {SkCanvas = new SKCanvas(new SKBitmap(1000, 800))};
            rc.RenderTarget = RenderTarget.Screen;
            
            plots = new PlotView[]
            {
                PlotView0,
                PlotView1,
                PlotView2
            };

            CalculationService service = null;
            //todo

            var calcThreadId =IntPtr.Zero;
            var calcThread = new Thread(()=>
            {
                service = new CalculationService();
                service.OnStart();
                calcThreadId = service.ThreadId;
            });
            calcThread.Priority = ThreadPriority.AboveNormal;
            calcThread.IsBackground = true;
            calcThread.Start();
            
            // Task<uint> t = Task<uint>.Factory.StartNew(() =>
            // {
            //     service = new CalculationService();
            //     service.OnStart();
            //     return service.ThreadId;
            // }, TaskCreationOptions.LongRunning);
            // uint calcThreadId = t.Result;
            calcThread.Join();
            PerfControl.Content = new PerformanceControl(GetCurrentThread(), calcThreadId);

            for (int i = 0; i < plots.Length; i++)
            {
                plots[i].Model = service?.PlotModels[i];
            }
            
            _dTimer = new DispatcherTimer(DispatcherPriority.Send);
            _dTimer.Interval = TimeSpan.FromMilliseconds(100);
            _dTimer.Tick += SignalPlot;
            _dTimer.IsEnabled = true;
        }
        
        private void SignalPlot(object sender, EventArgs e)
        {
            for (int i = 0; i < plots.Length; i++)
            {
                plots[i].InvalidatePlot(true);
            }
        }
    }
}