using System.Threading.Tasks;

public interface IPdfRender
{
    Task<byte[]> RenderSpecificationSheetPdf(string html);
}