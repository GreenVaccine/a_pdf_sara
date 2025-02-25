using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    public static async Task Main(string[] args)
    {
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exitCode != 0)
        {
            throw new Exception($"Playwright exited with code {exitCode} on install");
        }

        IPdfRender renderer = new InProcessPdfRender();

        foreach (var htmlFile in Directory.GetFiles(Directory.GetCurrentDirectory(), "*.html"))
        {
            string html = File.ReadAllText(htmlFile);
            byte[] pdfBytes = await renderer.RenderSpecificationSheetPdf(html);

            string pdfFile = Path.ChangeExtension(htmlFile, ".pdf");
            File.WriteAllBytes(pdfFile, pdfBytes);
            Console.WriteLine($"Generated PDF: {pdfFile}");
        }
    }
}