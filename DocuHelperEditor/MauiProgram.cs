using DocuHelper;
using DocuHelper.Agents;
using DocuHelper.Editors;
using DocuHelper.Importers;
using DocuHelper.Importers.Hwp;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
#if WINDOWS
using DocuHelperEditor.WinUI;
#endif

namespace DocuHelperEditor
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            AppPaths.CopyLegacyData();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton(CreateWorkspace());
            builder.Services.AddSingleton<DocuImporter, WordImporter>();
            builder.Services.AddSingleton<DocuImporter, ExcelImporter>();
            builder.Services.AddSingleton<DocuImporter, PptImporter>();
            builder.Services.AddSingleton<DocuImporter, HwpImporter>();

            builder.Services.AddSingleton<IPaperMapper, ExcelPaperMapper>();
            builder.Services.AddSingleton<IPaperMapper, WordPaperMapper>();
            builder.Services.AddSingleton<IPaperMapper, PptPaperMapper>();
            builder.Services.AddSingleton<IPaperMapper, HwpPaperMapper>();

            builder.Services.AddSingleton<WorkspaceRegistry>();
            builder.Services.AddSingleton<WorkspaceStore>();
            builder.Services.AddSingleton<AssistantStore>();
            builder.Services.AddSingleton<SettingsStore>();
            builder.Services.AddSingleton<UpdateStore>();

            builder.Services.AddSingleton<IFileDialogService, FileDialogService>();
            builder.Services.AddSingleton<IConversationRepository, ConversationRepository>();
#if WINDOWS
            builder.Services.AddSingleton<IShellService, WindowsShellService>();
            builder.Services.AddSingleton<IUpdateService, VelopackUpdateService>();
            builder.Services.AddSingleton<ISecretStore, DpapiSecretStore>();
            builder.Services.AddSingleton<IOfficeInterop, OfficeComInterop>();
#endif

            return builder.Build();
        }

        private static Workspace CreateWorkspace()
        {
            var workspace = new Workspace { Agent = new ClaudeAgent() };
            workspace.RegisterEditor(new WordEditor());
            workspace.RegisterEditor(new ExcelEditor());
            workspace.RegisterEditor(new PptEditor());
            workspace.RegisterEditor(new HwpEditor(new HancomOffice()));
            return workspace;
        }
    }
}
