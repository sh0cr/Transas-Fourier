﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using NickStrupat;
using OxyPlot;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;
using SkiaSharp;

namespace FourierTransas
{
    public partial class MemoryControl : UserControl
    {
        private DispatcherTimer _dTimer;
        private ComputerInfo _info = new ComputerInfo();
        private List<DataPoint> points;
        
        public MemoryControl()
        {
            InitializeComponent();
            SkiaRenderContext rc = new SkiaRenderContext() {SkCanvas = new SKCanvas(new SKBitmap(400, 200))};
            rc.RenderTarget = RenderTarget.Screen;

            RamPlotView.Model = new PlotModel()
            {
                Title = "Memory",
                IsLegendVisible = true,
                Series = {new LineSeries() {Title="% RAM", Color = OxyColors.Red, Decimator = Decimator.Decimate}}
            };
            points = (RamPlotView.Model.Series[0] as LineSeries).Points;
            RamPlotView.Model.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.BottomCenter,
                LegendFontSize = 12
            });
            (RamPlotView.Model as IPlotModel).Render(rc, RamPlotView.Model.PlotArea);
            _dTimer = new DispatcherTimer(DispatcherPriority.Render);
            _dTimer.Interval = TimeSpan.FromMilliseconds(200);
            _dTimer.Tick += (obj, e) =>
            {
                Task.Factory.StartNew(ResourceUsagePlot);
            };
            _dTimer.Start();
        }

        private int x = 0;

        private void ResourceUsagePlot()
        {
            points.Add(new DataPoint(x,100 * Environment.WorkingSet / (long) _info.TotalPhysicalMemory));
            x++;
            RamPlotView.InvalidatePlot(true);
        }
    }
}