﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using EnvDTE80;
using Microsoft.Build.Evaluation;
using System.Reflection;
using BuildProject = Microsoft.Build.Evaluation.Project;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Designers;
using Microsoft.VisualStudio.VCProjectEngine;

using DTEProject = EnvDTE.Project;

namespace SolutionConfigurationName
{
#if DEBUG
    extern alias VC;
    using VCProjectShim = VC::Microsoft.VisualStudio.Project.VisualC.VCProjectEngine.VCProjectShim;
#endif

    partial class MainSite
    {
        private static volatile bool _VCProjectCollectionLoaded;

        static MainSite()
        {
            _VCProjectCollectionLoaded = false;
        }

        public static void EnsureVCProjectsPropertiesConfigured(IVsHierarchy hiearchy)
        {
            if (_VCProjectCollectionLoaded)
                return;

            DTEProject project = hiearchy.GetProject();
            if (project == null || !(project.Object is VCProject))
                return;

            SolutionConfiguration2 configuration =
                (SolutionConfiguration2)_DTE2.Solution.SolutionBuild.ActiveConfiguration;

            // This is the first VC Project loaded, so we don't need to take
            // measures to ensure all projects are correctly marked as dirty
            SetVCProjectsConfigurationProperties(project, configuration.Name, configuration.PlatformName, false);
        }

        private static async void SetVCProjectsConfigurationProperties(DTEProject project,
            string configurationName, string platformName, bool allprojects)
        {
            // Inspired from Nuget: https://github.com/Haacked/NuGet/blob/master/src/VisualStudio12/ProjectHelper.cs
            IVsBrowseObjectContext context = project.Object as IVsBrowseObjectContext;
            UnconfiguredProject unconfiguredProject = context.UnconfiguredProject;
            IProjectLockService service = unconfiguredProject.ProjectService.Services.ProjectLockService;

            using (ProjectWriteLockReleaser releaser = await service.WriteLockAsync())
            {
                ProjectCollection collection = releaser.ProjectCollection;

                BuildProject buildproj = null;
                if (!allprojects)
                {
                    await releaser.CheckoutAsync(unconfiguredProject.FullPath);
                    ConfiguredProject configuredProject = await unconfiguredProject.GetSuggestedConfiguredProjectAsync();
                    buildproj = await releaser.GetProjectAsync(configuredProject);
                }

                ConfigureCollection(collection, buildproj, configurationName, platformName);

                _VCProjectCollectionLoaded = true;

                await releaser.ReleaseAsync();
            }
        }

        public static void SetVCProjectsConfigurationProperties(string configurationName, string platformName)
        {
            foreach (DTEProject project in _DTE2.Solution.Projects)
            {
                if (!(project.Object is VCProject))
                    continue;

                SetVCProjectsConfigurationProperties(project, configurationName, platformName, true);
#if DEBUG
                // The VCProject should be dirty when switching soulution configuration
                VCProjectShim shim = project.Object as VCProjectShim;
                bool test = shim.IsDirty;
#endif
                break;
            }
        }

        /* Alternative method to obtain the VCProject(s) collection
        private static void foo()
        {
            Type type = typeof(VCProjectEngineShim);
            object engine = type.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null, null);
            if (engine == null)
                return;

            ProjectCollection collection = (ProjectCollection)type.GetProperty("ProjectCollection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(engine, null);

            IProjectLockService service = (IProjectLockService)type.GetProperty("ProjectLockService", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(engine, null);
        }
        */
    }
}
