﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using NickStrupat;
using OxyPlot;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using Timer = System.Timers.Timer;

namespace FourierTransas
{
    /// <summary>
    /// сервис для мониторинга и ограничения потребления ресурсов
    /// </summary>
    public class MonitorService : IDisposable
    {
        public Services.CalculationService CalculationService { get; private set; }
        private Timer _timer;
        static PerformanceCounter _cpuCounter =
            new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
        static ComputerInfo _info = new ComputerInfo();
        Process _process = Process.GetCurrentProcess();
        
        private List<DataPoint> _cpuSamples;
        private List<DataPoint> _ramSamples;
        private List<List<DataPoint>> _threadSamples;
        private List<BarItem> _items;

        public PlotModel ThreadModel { get; private set; }
        public PlotModel RamModel { get; private set; }
        public PlotModel BarModel { get; private set; }
        
        public double CounterValue { get; private set; }
        private Func<uint> _mainThreadId;

        public MonitorService(Services.CalculationService service, Func<uint> getThreadId)
        {
            CalculationService = service;
            _mainThreadId = getThreadId;
            ThreadModel = new PlotModel
            {
                Title = "CPU",
                IsLegendVisible = true,
                Series =
                {
                    new LineSeries() {Title = "Total CPU", Color = OxyColors.Green, Decimator = Decimator.Decimate}
                }
            };
            ThreadModel.Series.Add(new LineSeries()
                {Color = OxyColors.Orange, Title = "plot render", Decimator = Decimator.Decimate});
            ThreadModel.Series.Add(new LineSeries()
                {Color = OxyColors.Blue, Title = "recource monitor", Decimator = Decimator.Decimate});
            ThreadModel.Series.Add(new LineSeries()
                {Color = OxyColors.Brown, Title = "calculation", Decimator = Decimator.Decimate});

            ThreadModel.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.BottomCenter,
                LegendFontSize = 12
            });
            _cpuSamples = (ThreadModel.Series[0] as LineSeries).Points;
            _threadSamples = new List<List<DataPoint>>(ThreadModel.Series.Count - 1);
            for (int i = 1; i < ThreadModel.Series.Count; i++)
            {
                _threadSamples.Add((ThreadModel.Series[i] as LineSeries).Points);
            }

            RamModel = new PlotModel()
            {
                Title = "Memory",
                IsLegendVisible = true,
                Series = {new LineSeries() {Title = "% RAM", Color = OxyColors.Red, Decimator = Decimator.Decimate}}
            };
            RamModel.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.BottomCenter,
                LegendFontSize = 12
            });
            _ramSamples = (RamModel.Series[0] as LineSeries).Points;

            // BarModel = new PlotModel()
            // {
            //     Series = {new BarSeries() {Items = {new BarItem(0), new BarItem(0)}}},
            //     Axes =
            //     {
            //         new CategoryAxis()
            //         {
            //             Position = AxisPosition.Left,
            //             Key = "ResourceAxis",
            //             ItemsSource = new[] {"Mem", "CPU"}
            //         }
            //     }
            // };
            // _items = (BarModel.Series[0] as BarSeries).Items;

            _timer = new Timer(1000);
            _timer.Elapsed += CpuRamUsage;
            _timer.Elapsed += CheckCPULimit;
        }

        public void OnStart()
        {
            Thread.BeginThreadAffinity();
            _timer.Enabled = true;
        }

        public void OnStop()
        {
            _timer.Enabled = false;
            
            var series = ThreadModel.Series;
            var legends = ThreadModel.Legends;
            var ramSeries = RamModel.Series;
            var ramLegend = RamModel.Legends;
            
            ThreadModel = new PlotModel();
            var cpuSeries = new LineSeries();
            cpuSeries.Points.AddRange(_cpuSamples);
            ThreadModel.Series.Add(new LineSeries()
            {
                ItemsSource = _cpuSamples,
                Title = series[0].Title,
                Decimator = Decimator.Decimate,
                Color = OxyColors.Green
            });
            for (int i=1;i<series.Count;i++)
            {
                ThreadModel.Series.Add(new LineSeries()
                {
                    ItemsSource = _threadSamples[i-1],
                    Decimator = Decimator.Decimate,
                    Color = (series[i] as LineSeries).Color,
                    Title = series[i].Title
                });
            }
            
//legends
            RamModel = new PlotModel();
            RamModel.Series.Add(new LineSeries() {ItemsSource = _ramSamples});
        }

        public void Dispose()
        {
            _timer.Enabled = false;
        }

        [DllImport("Kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public static double CurrentMemoryLoad()
        {
            return 100 * Environment.WorkingSet / (long) _info.TotalPhysicalMemory;
        }

        public static double CurrentCpuLoad()
        {
            return _cpuCounter.NextValue() / Environment.ProcessorCount;
        }

        private void CpuRamUsage(object sender, ElapsedEventArgs e)
        {
            _cpuCounter.NextValue();
            var pThreads = _process.Threads.Cast<ProcessThread>();
            var processThread = pThreads.First(p => p.Id == GetCurrentThreadId());
            
            var mainThread = pThreads.First(p => p.Id == _mainThreadId());
            
            var t1 = processThread.TotalProcessorTime;
            var p1 = _process.TotalProcessorTime;
            int x = _ramSamples.Count + 1;
            _ramSamples.Add(new DataPoint(x, 100 * Environment.WorkingSet / (long) _info.TotalPhysicalMemory));
            lock (ThreadModel.SyncRoot)
            {
                _threadSamples[0].Add(new DataPoint(x,
                    100 * mainThread.UserProcessorTime/_process.UserProcessorTime));
                _threadSamples[2].Add(new DataPoint(x,
                    100 * CalculationService.CounterValue));
            }

            CounterValue = (processThread.UserProcessorTime - t1) / (_process.UserProcessorTime - p1) / Environment.ProcessorCount;
            lock (ThreadModel.SyncRoot)
            {
                _threadSamples[1].Add(new DataPoint(x, 100 * CounterValue));
            }
//todo: msbuild
            _cpuSamples.Add(new DataPoint(x, _cpuCounter.NextValue() / Environment.ProcessorCount));
        }

        public double CpuLimit { get; set; } = 5;

        private void CheckCPULimit(object sender, EventArgs e)
        {
            Func<double, double> f = d =>
            {
                _timer.Interval = d;
                return CounterValue - CpuLimit;
            };
            _timer.Interval = MathNet.Numerics.RootFinding.Bisection.FindRoot(f, 200, 1000, 3, 3);
        }
    }
}