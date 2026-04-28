using System.IO;
using System.Reflection;
using Markdig;

namespace SimulatorProject.Help;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public static bool ResourceExists(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        return stream != null;
    }

    public static string LoadMarkdown(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string RenderHtml(string markdown, string title)
    {
        var bodyHtml = Markdown.ToHtml(markdown, Pipeline);
        return BuildHtmlTemplate(title, bodyHtml);
    }

    public static string RenderPlaceholder(string title, string message)
    {
        var body = $"<h1>{title}</h1><p>{message}</p>";
        return BuildHtmlTemplate(title, body);
    }

    private static string BuildHtmlTemplate(string title, string body) => $$"""
        <!DOCTYPE html>
        <html lang="ko">
        <head>
        <meta charset="utf-8">
        <title>{{title}}</title>
        <script src="https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js"></script>
        <style>
          :root { color-scheme: dark; }
          html, body {
            background: #1a1a2e;
            color: #e8e8f0;
            font-family: -apple-system, "Segoe UI", "맑은 고딕", "Apple SD Gothic Neo", sans-serif;
            font-size: 15px;
            line-height: 1.7;
            margin: 0;
            padding: 32px 48px 64px 48px;
          }
          h1, h2, h3, h4 { color: #ffffff; font-weight: 700; line-height: 1.3; }
          h1 { font-size: 28px; border-bottom: 2px solid #3a3a5a; padding-bottom: 10px; margin-top: 40px; }
          h2 { font-size: 22px; margin-top: 36px; color: #c4b5fd; }
          h3 { font-size: 17px; margin-top: 24px; color: #a5b4fc; }
          p { margin: 12px 0; }
          a { color: #818cf8; }
          a:hover { color: #a5b4fc; }
          code {
            background: #2a2a44; color: #fbbf24;
            padding: 2px 6px; border-radius: 4px;
            font-family: "Cascadia Code", Consolas, monospace; font-size: 13px;
          }
          pre {
            background: #0f0f1f; border: 1px solid #2a2a4a; border-radius: 8px;
            padding: 14px 18px; overflow-x: auto;
          }
          pre code { background: transparent; color: #e8e8f0; padding: 0; font-size: 13px; }
          table {
            border-collapse: collapse; width: 100%;
            margin: 16px 0; background: #1f1f3a;
          }
          th, td { border: 1px solid #3a3a5a; padding: 10px 14px; text-align: left; }
          th { background: #2a2a44; color: #c4b5fd; font-weight: 600; }
          tr:nth-child(2n) td { background: #232340; }
          blockquote {
            border-left: 4px solid #818cf8; background: #1f1f3a;
            margin: 14px 0; padding: 8px 16px; color: #c5c5d4;
          }
          hr { border: 0; border-top: 1px solid #3a3a5a; margin: 28px 0; }
          ul, ol { padding-left: 28px; }
          li { margin: 4px 0; }
          .mermaid {
            background: #0f0f1f; border-radius: 8px; padding: 16px;
            margin: 16px 0; text-align: center;
          }
        </style>
        </head>
        <body>
        {{body}}
        <script>
          // Markdig renders mermaid code blocks as <pre><code class="language-mermaid"> -> convert to div.mermaid
          document.querySelectorAll('pre > code.language-mermaid').forEach(el => {
              const div = document.createElement('div');
              div.className = 'mermaid';
              div.textContent = el.textContent;
              el.parentElement.replaceWith(div);
          });
          mermaid.initialize({ startOnLoad: true, theme: 'dark', securityLevel: 'loose' });
        </script>
        </body>
        </html>
        """;
}
