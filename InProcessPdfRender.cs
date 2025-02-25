using System.Threading.Tasks;
using Microsoft.Playwright;
using System.IO;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;

public class InProcessPdfRender : IPdfRender
{
	public async Task<byte[]> RenderSpecificationSheetPdf(string html)
	{
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
		var page = await browser.NewPageAsync();

		// Set the page content
		await page.SetContentAsync(html);

		// Remove all `.divider` elements before generating the PDF
		await page.EvaluateAsync(@"() => document.querySelectorAll('.divider').forEach(div => div.remove());");

		// Process `.frame-19` sections and track `lastValidPage`
		var lastValidPage = await page.EvaluateAsync<int>(@"
            () => {
                const pageHeight = 1122; // A4 at 96 DPI
                let usedHeight = 250;  // First-page margin
                const margin = 130;
                let lastPage = 1;

                document.querySelectorAll('.frame-19').forEach(frame => {
                    frame.style.backgroundColor = 'transparent';

                    let sections = Array.from(frame.children)
                        .filter(child => child.tagName === 'DIV' && child.textContent.trim().length > 0);

                    sections.forEach((section, index) => {
                        section.style.padding = '10px 10px 30px 10px';
                        section.style.backgroundColor = 'white';
                        
                        let sectionHeight = section.getBoundingClientRect().height + 40;

                        // Handle oversized sections that exceed a full page
                        if (sectionHeight >= pageHeight) {
                            lastPage += Math.ceil(sectionHeight / pageHeight);
                            section.style.pageBreakBefore = 'always';
                            usedHeight = sectionHeight % pageHeight;
                        }
                        // Move section to next page if needed
                        else if (usedHeight + sectionHeight + margin >= pageHeight) {
                            if (index !== 0) section.style.pageBreakBefore = 'always';
                            usedHeight = sectionHeight;
                            lastPage++;
                        } 
                        // Section fits on the current page
                        else {
                            usedHeight += sectionHeight;
                        }
                    });
                });

                return lastPage;
            }");

		// Ensure proper width and print settings
		await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Microsoft.Playwright.Media.Print });

		var pdfBytes = await page.PdfAsync(new PagePdfOptions
		{
			Width = "1440px",
			PrintBackground = true
		});

		await page.CloseAsync();
		await browser.CloseAsync();

		// Backup method to determine `lastValidPage` if JavaScript fails
		if (lastValidPage <= 0)
		{
			lastValidPage = GetLastValidPageFromPdf(pdfBytes);
		}

		// Trim the PDF to only valid pages
		var result = TrimPDF(pdfBytes, lastValidPage);
		return result;
	}

	private byte[] TrimPDF(byte[] pdfBytes, int lastValidPage)
	{
		using var inputStream = new MemoryStream(pdfBytes);
		using var outputStream = new MemoryStream();
		using var reader = new PdfReader(inputStream);
		using var writer = new PdfWriter(outputStream);
		using var pdfDoc = new PdfDocument(reader);
		using var newPdfDoc = new PdfDocument(writer);

		var merger = new PdfMerger(newPdfDoc);
		merger.Merge(pdfDoc, 1, lastValidPage);

		newPdfDoc.Close(); // Ensure all writes are committed
		return outputStream.ToArray();
	}

	private int GetLastValidPageFromPdf(byte[] pdfBytes)
	{
		using var inputStream = new MemoryStream(pdfBytes);
		using var reader = new PdfReader(inputStream);
		using var document = new PdfDocument(reader);
		return document.GetNumberOfPages();
	}
}