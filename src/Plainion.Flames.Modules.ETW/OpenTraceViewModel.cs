﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using Prism.Mvvm;
using Plainion.Flames.Infrastructure.Controls;
using Plainion.Prism.Interactivity.InteractionRequest;
using Plainion.Progress;

namespace Plainion.Flames.Modules.ETW
{
    class OpenTraceViewModel : BindableBase, IInteractionRequestAware
    {
        private string mySymbolPath;
        private bool myUseDefaultWebProxy;
        private bool myShowMissingEvents;
        private bool myHasCpuSamples;
        private bool myHasCSwitches;
        private bool myStartStopEventsOnly;
        private LoadSettings myModel;

        public OpenTraceViewModel( LoadSettings loadSettings )
        {
            Contract.RequiresNotNull( loadSettings, "loadSettings" );

            myModel = loadSettings;

            SymbolPath = myModel.SymbolPath;
            UseDefaultWebProxy = myModel.UseDefaultWebProxy;

            LoadCommand = new DelegateCommand( OnLoad );
            CancelCommand = new DelegateCommand( OnCancel );
        }

        public ICommand LoadCommand { get; private set; }

        private void OnLoad()
        {
            myModel.SymbolPath = SymbolPath;
            myModel.UseDefaultWebProxy = UseDefaultWebProxy;
            myModel.StartStopEventsOnly = StartStopEventsOnly;

            myModel.SelectedProcesses.Clear();
            foreach( var process in TracesTreeSource.Processes )
            {
                if( process.IsVisible == true )
                {
                    myModel.SelectedProcesses.Add( process.ProcessId );
                }
            }

            Notification.TrySetConfirmed( true );
            FinishInteraction();
        }

        public ICommand CancelCommand { get; private set; }

        private void OnCancel()
        {
            Notification.TrySetConfirmed( false );
            FinishInteraction();
        }

        public string SymbolPath
        {
            get { return mySymbolPath; }
            set { SetProperty( ref mySymbolPath, value ); }
        }

        public bool UseDefaultWebProxy
        {
            get { return myUseDefaultWebProxy; }
            set { SetProperty( ref myUseDefaultWebProxy, value ); }
        }

        public TracesTree TracesTreeSource { get; private set; }

        public Visibility SymbolSettingsVisibility { get; private set; }

        public bool StartStopEventsOnly
        {
            get { return myStartStopEventsOnly; }
            set
            {
                if( SetProperty( ref myStartStopEventsOnly, value ) )
                {
                    EvaluateMissingEvents();
                }
            }
        }

        public bool ShowMissingEvents
        {
            get { return myShowMissingEvents; }
            set
            {
                if( SetProperty( ref myShowMissingEvents, value ) )
                {
                    OnPropertyChanged( () => MissingEvents );
                }
            }
        }

        public string MissingEvents
        {
            get
            {
                var events = new List<string>( 2 );
                if( !myHasCpuSamples )
                {
                    events.Add( "CPU samples" );
                }
                if( !myHasCSwitches )
                {
                    events.Add( "CSwitches" );
                }

                return string.Join( ",", events );
            }
        }

        internal static OpenTraceViewModel Create( TraceFile traceFile, LoadSettings model, IProgress<IProgressInfo> progress )
        {
            var dataContext = new OpenTraceViewModel( model );

            if( traceFile.EtlxExists )
            {
                dataContext.SymbolSettingsVisibility = Visibility.Collapsed;
            }
            else
            {
                dataContext.SymbolSettingsVisibility = Visibility.Visible;
            }

            dataContext.Initialize( traceFile, progress );

            return dataContext;
        }

        private void Initialize( TraceFile traceFile, IProgress<IProgressInfo> progress )
        {
            var processes = new Dictionary<int, TraceProcessNode>();

            if( File.Exists( traceFile.Etl ) )
            {
                GetProcessesFromEtl( traceFile.Etl, processes, progress );
            }
            else
            {
                // handle the case that only ETLX was saved
                GetProcessesFromEtlx( traceFile.Etlx, processes, progress );
            }

            EvaluateMissingEvents();

            TracesTreeSource = new TracesTree
            {
                Processes = processes.Values
                    .OrderBy( p => p.Name )
                    .ThenBy( p => p.ProcessId )
                    .ToList()
            };

            if( myModel.SelectedProcesses.Any() )
            {
                foreach( var process in processes.Values )
                {
                    process.IsVisible = myModel.SelectedProcesses.Contains( process.ProcessId );
                }
            }
        }

        private void EvaluateMissingEvents()
        {
            ShowMissingEvents = !( myHasCSwitches && myHasCpuSamples ) && !StartStopEventsOnly;
        }

        private void GetProcessesFromEtl( string etl, Dictionary<int, TraceProcessNode> processes, IProgress<IProgressInfo> progress )
        {
            using( var source = new ETWTraceEventSource( etl ) )
            {
                var progressInfo = new PercentageProgress( "Step 1: Getting all processes", source.SessionDuration.TotalMilliseconds );
                progress.Report( progressInfo );

                Action<ProcessTraceData> OnProcessStartEnd = e =>
                {
                    progressInfo.Value = e.TimeStampRelativeMSec;
                    progress.Report( progressInfo );

                    AddProcess( processes, e );
                };

                source.Kernel.ProcessStartGroup += OnProcessStartEnd;
                source.Kernel.ProcessEndGroup += OnProcessStartEnd;

                Action<SampledProfileTraceData> OnCpuSample = null;
                OnCpuSample = e =>
                {
                    myHasCpuSamples = true;
                    source.Kernel.PerfInfoSample -= OnCpuSample;
                };
                source.Kernel.PerfInfoSample += OnCpuSample;

                Action<CSwitchTraceData> OnCSwitch = null;
                OnCSwitch = e =>
                {
                    myHasCSwitches = true;
                    source.Kernel.ThreadCSwitch -= OnCSwitch;
                };
                source.Kernel.ThreadCSwitch += OnCSwitch;

                source.Process();

                progressInfo.Value = progressInfo.Maximum;
                progress.Report( progressInfo );
            }
        }

        private static void AddProcess( Dictionary<int, TraceProcessNode> processes, ProcessTraceData e )
        {
            if( !processes.ContainsKey( e.ProcessID ) )
            {
                processes[ e.ProcessID ] = new TraceProcessNode
                {
                    ProcessId = e.ProcessID,
                    Name = e.ImageFileName
                };
            }
        }

        private void GetProcessesFromEtlx( string etlx, Dictionary<int, TraceProcessNode> processes, IProgress<IProgressInfo> progress )
        {
            var source = TraceLog.OpenOrConvert( etlx );

            var progressInfo = new PercentageProgress( "Step 1: Getting all processes", source.SessionDuration.TotalMilliseconds );
            progress.Report( progressInfo );

            foreach( var p in source.Processes )
            {
                processes[ p.ProcessID ] = new TraceProcessNode
                {
                    ProcessId = p.ProcessID,
                    Name = p.ImageFileName
                };
            }

            foreach( var evt in source.Events )
            {
                if( evt is SampledProfileIntervalTraceData )
                {
                    myHasCpuSamples = true;
                }
                else if( evt is CSwitchTraceData )
                {
                    myHasCSwitches = true;
                }

                if( myHasCpuSamples && myHasCSwitches )
                {
                    break;
                }
            }

            progressInfo.Value = progressInfo.Maximum;
            progress.Report( progressInfo );
        }

        public Action FinishInteraction { get; set; }

        public INotification Notification { get; set; }
    }
}
