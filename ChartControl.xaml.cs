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
        private CalculationService _service;
        
        [DllImport("Kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        
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
            _service = new CalculationService();
    
            var calcThread = new Thread(_service.OnStart);
            calcThread.Priority = ThreadPriority.AboveNormal;
            calcThread.IsBackground = true;
            calcThread.Start();

            PerfControl.Content = new PerformanceControl(GetCurrentThreadId(), _service.ThreadId);

            for (int i = 0; i < plots.Length; i++)
            {
                plots[i].Model = _service.PlotModels[i];
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