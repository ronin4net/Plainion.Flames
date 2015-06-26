﻿using System.ComponentModel.Composition;
using System.Windows.Input;
using Plainion.Flames.Controls;
using Plainion.Flames.Presentation;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Interactivity.InteractionRequest;
using Microsoft.Practices.Prism.Mvvm;
using Microsoft.Practices.Prism.Regions;
using Plainion.Flames.Infrastructure.Services;
using System;
using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;

namespace Plainion.Flames.Viewer.ViewModels
{
    // https://rachel53461.wordpress.com/2011/12/
    [Export]
    class FlamesBrowserViewModel : BindableBase
    {
        private FlameSetPresentation myPresentation;
        private bool myFlamesVisible;
        private IProjectService myProjectService;

        [ImportingConstructor]
        internal FlamesBrowserViewModel( IProjectService projectService )
        {
            myProjectService = projectService;
            myProjectService.ProjectChanged += OnProjectChanged;
            myFlamesVisible = true;

            Settings = new FlamesSettingsViewModel();

            ExpandCollapseCommand = new DelegateCommand<FlameHeader>( h => h.Flame.IsExpanded = !h.Flame.IsExpanded );
            HideCommand = new DelegateCommand<FlameHeader>( h => h.Flame.Visibility = ContentVisibility.Invisible );
            RenameCommand = new DelegateCommand<FlameHeader>( h => h.IsInEditMode = true );
            ZoomHomeCommand = new DelegateCommand( OnZoomHome );
            ZoomInCommand = new DelegateCommand( OnZoomIn );
            ZoomOutCommand = new DelegateCommand( OnZoomOut );
            ClearSelectionsCommand = new DelegateCommand( ClearSelections );

            ToggleViewCommand = new DelegateCommand( () => FlamesVisible = !FlamesVisible );
            SpawnSettingsWindowCommand = new DelegateCommand( OnSpawnSettingsWindow );
            SpawnSettingsRequest = new InteractionRequest<Notification>();
        }

        private void OnProjectChanged( object sender, EventArgs e )
        {
            Presentation = myProjectService.Project.Presentation;

            PropertyChangedEventManager.AddHandler( myProjectService.Project, OnPresentationChanged,
                PropertySupport.ExtractPropertyName( () => myProjectService.Project.Presentation ) );

            if( myProjectService.Project.WasDeserialized )
            {
                // http://stackoverflow.com/questions/13026826/execute-command-after-view-is-loaded-wpf-mvvm
                Application.Current.Dispatcher.BeginInvoke( DispatcherPriority.ApplicationIdle, new Action( () =>
                {
                    // we loaded user settings from disk which might filter out certain threads or calls.
                    // lets display settings window to the user so that it is more obvious that everything
                    // is visible in the flames.
                    SpawnSettingsWindowCommand.Execute( null );

                    // TODO: we want to navigate to process-threads-view but currently we cannot access viewmodel :(
                    Settings.SelectedTabIndex = 1;
                } ) );
            }
        }

        private void OnPresentationChanged( object sender, PropertyChangedEventArgs e )
        {
            Presentation = myProjectService.Project.Presentation;
        }

        public FlamesSettingsViewModel Settings { get; private set; }

        public FlameSetPresentation Presentation
        {
            get { return myPresentation; }
            set
            {
                if( SetProperty( ref myPresentation, value ) )
                {
                    Settings.Presentation = myPresentation;
                }
            }
        }

        public bool FlamesVisible
        {
            get { return myFlamesVisible; }
            set { SetProperty( ref myFlamesVisible, value ); }
        }

        public ICommand ExpandCollapseCommand { get; private set; }
        public ICommand HideCommand { get; private set; }
        public ICommand RenameCommand { get; private set; }
        public ICommand ZoomHomeCommand { get; private set; }
        public ICommand ZoomInCommand { get; private set; }
        public ICommand ZoomOutCommand { get; private set; }
        public ICommand ClearSelectionsCommand { get; private set; }

        public ICommand ToggleViewCommand { get; private set; }
        public ICommand SpawnSettingsWindowCommand { get; private set; }

        public InteractionRequest<Notification> SpawnSettingsRequest { get; private set; }

        private void OnZoomHome()
        {
            if( Presentation != null && FlamesVisible )
            {
                Presentation.TimelineViewport.Set( Presentation.TimelineViewport.Min, Presentation.TimelineViewport.Max );
            }
        }

        private void OnZoomIn()
        {
            if( Presentation != null && FlamesVisible )
            {
                Presentation.TimelineViewport.ZoomAtCenter( +0.1 );
            }
        }

        private void OnZoomOut()
        {
            if( Presentation != null && FlamesVisible )
            {
                Presentation.TimelineViewport.ZoomAtCenter( -0.1 );
            }
        }

        private void ClearSelections()
        {
            if( Presentation != null && FlamesVisible )
            {
                Presentation.Selections.Clear();
            }
        }

        private void OnSpawnSettingsWindow()
        {
            var notification = new Notification();
            notification.Title = "Settings";
            notification.Content = Settings;

            var selectedTabIndex = Settings.SelectedTabIndex;

            SpawnSettingsRequest.Raise( notification );

            // TODO: unfort. new view will first sync back the initial selected tab index into view model
            // -> we lose the selection :(
            Settings.SelectedTabIndex = selectedTabIndex;

            // switch back to flames
            FlamesVisible = true;
        }
    }
}
