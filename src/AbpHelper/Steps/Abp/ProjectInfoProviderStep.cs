﻿using EasyAbp.AbpHelper.Models;
using Elsa.Results;
using Elsa.Services.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasyAbp.AbpHelper.Workflow;

namespace EasyAbp.AbpHelper.Steps.Abp
{
    public class ProjectInfoProviderStep : StepWithOption
    {
        protected override async Task<ActivityExecutionResult> OnExecuteAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
        {
            var baseDirectory = await context.EvaluateAsync(BaseDirectory, cancellationToken);
            LogInput(() => baseDirectory);
            var excludeDirectories = await context.EvaluateAsync(ExcludeDirectories, cancellationToken);
            LogInput(() => excludeDirectories, string.Join("; ", excludeDirectories));

            TemplateType templateType;
            if (FileExistsInDirectory(baseDirectory, "*.DbMigrator.csproj", excludeDirectories))
            {
                templateType = TemplateType.Application;
            }
            else if (FileExistsInDirectory(baseDirectory, "*.Host.Shared.csproj", excludeDirectories))
            {
                templateType = TemplateType.Module;
            }
            else
            {
                throw new NotSupportedException($"Unknown ABP project structure. Directory: {baseDirectory}");
            }


            // Assume the domain project must be existed for an ABP project
            var domainCsprojFile = SearchFileInDirectory(baseDirectory, "*.Domain.csproj", excludeDirectories);
            if (domainCsprojFile == null) throw new NotSupportedException($"Cannot find the domain project file. Make sure it is a valid ABP project. Directory: {baseDirectory}");

            var fileName = Path.GetFileName(domainCsprojFile);
            var fullName = fileName.RemovePostFix(".Domain.csproj");

            UiFramework uiFramework;
            if (FileExistsInDirectory(baseDirectory, "*.cshtml", excludeDirectories))
            {
                uiFramework = UiFramework.RazorPages;
                if (templateType == TemplateType.Application)
                {
                    context.SetVariable(VariableNames.AspNetCoreDir, baseDirectory);
                }
                else
                {
                    context.SetVariable(VariableNames.AspNetCoreDir, baseDirectory);
                }
            }
            else if (FileExistsInDirectory(baseDirectory, "app.module.ts", excludeDirectories))
            {
                uiFramework = UiFramework.Angular;
                context.SetVariable(VariableNames.AspNetCoreDir, Path.Combine(baseDirectory, "aspnet-core"));
            }
            else
            {
                uiFramework = UiFramework.None;
                if (templateType == TemplateType.Application)
                {
                    context.SetVariable(VariableNames.AspNetCoreDir, Path.Combine(baseDirectory, "aspnet-core"));
                }
                else
                {
                    context.SetVariable(VariableNames.AspNetCoreDir, baseDirectory);
                }
            }

            CheckSlnExists(context, fullName);

            var tiered = false;
            if (templateType == TemplateType.Application)
            {
                tiered = FileExistsInDirectory(baseDirectory, "*.IdentityServer.csproj", excludeDirectories);
            }

            var projectInfo = new ProjectInfo(baseDirectory, fullName, templateType, uiFramework, tiered);

            context.SetLastResult(projectInfo);
            context.SetVariable("ProjectInfo", projectInfo);
            LogOutput(() => projectInfo);

            return Done();
        }

        private void CheckSlnExists(WorkflowExecutionContext context, string projectName)
        {
            string aspNetCoreDir = context.GetVariable<string>(VariableNames.AspNetCoreDir);
            string slnFile = Path.Combine(aspNetCoreDir, $"{projectName}.sln");
            if (!File.Exists(slnFile))
            {
                throw new FileNotFoundException($"The solution file {projectName}.sln is not found in '{aspNetCoreDir}'. Make sure you specific the right folder.");
            }
        }
    }
}
