﻿// Copyright (c) 2014 Francesco Pretto
// This file is subject to the MIT license

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
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
using Microsoft.VisualStudio.VCProjectEngine;
using ATask = System.Threading.Tasks.Task;
using BuildProject = Microsoft.Build.Evaluation.Project;
using DTEProject = EnvDTE.Project;

namespace SolutionConfigurationName
{
    partial class MainSite
    {
        private static AsyncLock _VCLock;
        private static bool _VCProjectCollectionLoaded;     // Lock guarded

        static MainSite()
        {
            _VCProjectCollectionLoaded = false;
            _VCLock = new AsyncLock();
        }

        public static async void EnsureVCProjectsPropertiesConfigured(IVsHierarchy hierarchy)
        {
            DTEProject project = hierarchy.GetProject();
            if (project == null || project.GetKindGuid() != VSConstants.UICONTEXT.VCProject_guid)
                return;

            if (VersionGreaterEqualTo(DTEVersion.VS15))
            {
                // VS2017 needs project to be invalidated after loaded to apply the
                // global properties, supplied with MEF component. Trying to save the
                // project at this point may result in a crash if the project is inside
                // a Solution Folder (why???). Fortunately, at this point to invalidate
                // the project is not neeeded to save
                InvalidateProject(project, false);
                return;
            }

            using (await _VCLock.LockAsync())
            {
                if (_VCProjectCollectionLoaded)
                    return;

                SolutionConfiguration2 configuration =
                    (SolutionConfiguration2)_DTE2.Solution.SolutionBuild.ActiveConfiguration;

                // When creating a completely new project the solution doesn't exist yet
                // so the ActiveConfiguration is null
                if (configuration == null)
                    return;

                // This is the first VC Project loaded, so we don't need to take
                // measures to ensure all projects are correctly marked as dirty
                await SetVCProjectsConfigurationProperties(project, configuration.Name, configuration.PlatformName);
            }
        }

        private static async ATask SetVCProjectsConfigurationProperties(DTEProject project,
            string configurationName, string platformName)
        {
            switch (_Version)
            {
#if false
#if VS12
                case DTEVersion.VS12:
                    await SetVCProjectsConfigurationProperties12(project, configurationName, platformName);
                    break;
                case DTEVersion.VS14:
                    await SetVCProjectsConfigurationProperties14(project, configurationName, platformName);
                    break;
#endif
#endif
                default:
                    throw new Exception();
            }
        }

        private static async void SetVCProjectsConfigurationProperties(string configurationName, string platformName)
        {
            foreach (DTEProject project in _DTE2.Solution.Projects.AllProjects())
            {
                // Don't test for instance of VCProject, doesn't work for plugin loaded in VS2015
                if (project.GetKindGuid() != VSConstants.UICONTEXT.VCProject_guid)
                    continue;

                await SetVCProjectsConfigurationProperties(project, configurationName, platformName);

                break;
            }

#if DEBUG
            foreach (DTEProject project in _DTE2.Solution.Projects.AllProjects())
            {
                VCProject vcproject = project.Object as VCProject;
                if (vcproject == null)
                    continue;

                bool test = vcproject.IsDirty;
            }
#endif
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
