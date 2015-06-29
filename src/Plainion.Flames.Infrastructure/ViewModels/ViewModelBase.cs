﻿using System;
using System.ComponentModel;
using Microsoft.Practices.Prism.Mvvm;
using Plainion.Flames.Infrastructure.Services;
using Plainion.Flames.Presentation;

namespace Plainion.Flames.Infrastructure.ViewModels
{
    public class ViewModelBase : BindableBase
    {
        private FlameSetPresentation myPresentation;

        protected ViewModelBase(IProjectService projectService)
        {
            ProjectService = projectService;
            ProjectService.ProjectChanged += OnProjectChanged;
        }

        protected IProjectService ProjectService { get; private set; }

        private void OnProjectChanged(object sender, EventArgs e)
        {
            if (ProjectService.Project.Presentation != null)
            {
                Presentation = ProjectService.Project.Presentation;
            }

            PropertyChangedEventManager.AddHandler(ProjectService.Project, Project_PresentationChanged,
                PropertySupport.ExtractPropertyName(() => ProjectService.Project.Presentation));

            OnProjectChanged();
        }

        protected virtual void OnProjectChanged() { }

        private void Project_PresentationChanged(object sender, PropertyChangedEventArgs e)
        {
            Presentation = ProjectService.Project.Presentation;
        }

        public FlameSetPresentation Presentation
        {
            get { return myPresentation; }
            set
            {
                var oldPresentation = myPresentation;
                if (SetProperty(ref myPresentation, value))
                {
                    OnPresentationChanged(oldPresentation);
                }
            }
        }

        protected virtual void OnPresentationChanged(FlameSetPresentation oldValue) { }
    }
}
