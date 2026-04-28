using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace SimulatorProject.Help;

public partial class HelpWindow : Window
{
    private const string SetupResource    = "SimulatorProject.Resources.setup-guide.md";
    private const string ProtocolResource = "SimulatorProject.Resources.protocol-guide.md";

    private string _currentResource = SetupResource;
    private bool _ready;

    public HelpWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(Path.GetTempPath(), "DevSimulator", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await WebViewer.EnsureCoreWebView2Async(env);
            _ready = true;
            ShowResource(_currentResource);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WebView2 초기화 실패:\n{ex.Message}\n\n" +
                "Microsoft Edge WebView2 Runtime이 설치되어 있는지 확인하세요.\n" +
                "다운로드: https://developer.microsoft.com/microsoft-edge/webview2/",
                "DevSimulator 도움말", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
    }

    private void ShowResource(string resourceName)
    {
        if (!_ready) return;
        try
        {
            var title = resourceName.Contains("setup") ? "사용 가이드" : "프로토콜 명세";

            string html;
            if (MarkdownRenderer.ResourceExists(resourceName))
            {
                var md = MarkdownRenderer.LoadMarkdown(resourceName);
                html = MarkdownRenderer.RenderHtml(md, title);
            }
            else
            {
                html = MarkdownRenderer.RenderPlaceholder(
                    title,
                    "이 문서는 아직 준비 중입니다. 추후 업데이트를 기다려 주세요.");
            }

            WebViewer.NavigateToString(html);
            _currentResource = resourceName;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"문서 로드 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSetupGuide_Click(object sender, RoutedEventArgs e)    => ShowResource(SetupResource);
    private void BtnProtocolGuide_Click(object sender, RoutedEventArgs e) => ShowResource(ProtocolResource);

    private void BtnOpenExternal_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var fileName = _currentResource.Contains("setup") ? "setup-guide.md" : "protocol-guide.md";
            var docsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "docs", fileName);
            docsPath = Path.GetFullPath(docsPath);
            if (File.Exists(docsPath))
                Process.Start(new ProcessStartInfo(docsPath) { UseShellExecute = true });
            else
                MessageBox.Show($"파일을 찾을 수 없습니다:\n{docsPath}", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"브라우저 열기 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
