using System;
using System.IO;
using Avalonia.Platform;
using InfraSweep.App.Helpers;
using InfraSweep.App.Models;
using InfraSweep.App.Services.Interfaces;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace InfraSweep.App.Services;

public class ReportService : IReportService, IDisposable
{
    private const double LineHeight = 12;
    private const double LineSpacing = 6;
    private const double PageMargin = 42;
    private const double PageHeight = 842; // Standard A4 Page

    private static readonly XFont TextFont = new ("Inter", LineHeight, XFontStyleEx.Regular);
    private static readonly XFont TextFontBold = new ("Inter", LineHeight, XFontStyleEx.Bold);
    private static readonly XFont TitleFont = new ("Inter", LineHeight*2, XFontStyleEx.Bold);

    private PdfDocument? Document;
    private XGraphics? Graphics;
    private PdfPage? Page;
    private double CurrentY;

    public ReportService()
    {
        GlobalFontSettings.FontResolver = new AvaloniaFontResolver();
    }

    public void SaveReport(ScanResult scanResult, string filePath)
    {
        Document = new PdfDocument();

        string scanDateString = scanResult.TimeOfScan.ToString("g");

        Document.Info.Title = 
            $"{ResourceHelper.GetString("PdfDocumentTitle")} {scanDateString}";
        
        Page = Document.AddPage();
        Graphics = XGraphics.FromPdfPage(Page);

        CurrentY = PageMargin;

        DrawText(ResourceHelper.GetString("PdfDocumentTitle"), isTitle: true);
        DrawText(scanDateString, isTitle: true);
        
        DrawText("");

        foreach (var network in scanResult.Networks)
        {
            DrawText($"{ResourceHelper.GetString("PdfNetworkLabel")} {network.DisplayName}", isBold: true);

            foreach (var host in network.Hosts)
            {
                DrawText("");

                string friendlyName = string.IsNullOrEmpty(host.Host.FriendlyName) 
                    ? string.Empty 
                    : $"({host.Host.FriendlyName})";

                string hostHeader = $"\u2022 {ResourceHelper.GetString("PdfHostAddressLabel")} {host.Host.Address}";

                DrawText(hostHeader, isBold: true, indentLevel: 1);

                if (friendlyName.Length > 0)
                {
                    XSize size = Graphics.MeasureString($"{hostHeader} {friendlyName}", TextFontBold);

                    if (XUnit.FromPoint(size.ToXPoint().X) >= (XUnit.FromPoint(PageMargin * 2) + Page.Width))
                    {
                        DrawText($"  {friendlyName}", indentLevel: 1);
                    }
                    else
                    {
                        size = Graphics.MeasureString(hostHeader, TextFontBold);
                        DrawText($" {friendlyName}", 
                            useLastLine: true, 
                            indentLevel: 1, 
                            xOffset: (int)size.ToXPoint().X);
                    }
                }

                double lastY = CurrentY;

                if (host.HostVulnerabilities?.Cves.Count > 0)
                {
                    DrawText($"- {ResourceHelper.GetString("PdfHostVulnerabilitiesLabel")}", indentLevel: 2, isBold: true);

                    foreach (var cve in host.HostVulnerabilities.Cves)
                    {
                        DrawText($"\u25E6 {cve.CveId}", indentLevel: 3);
                    }
                }

                if (host.FoundSoftware?.Count > 0)
                {
                    DrawText($"- {ResourceHelper.GetString("PdfFoundSoftwareLabel")}", indentLevel: 2, isBold: true);

                    foreach (var software in host.FoundSoftware)
                    {
                        string softwareName = string.IsNullOrEmpty(software.Software.Name) 
                            ? ResourceHelper.GetString("PdfUnknownService")
                            : software.Software.Name;
                        
                        string version = string.IsNullOrEmpty(software.Software.Version)
                            ? string.Empty
                            : " " + software.Software.Version;
                        
                        DrawText($"▪ {softwareName}{version} {ResourceHelper.GetString("PdfSoftwarePortLabel")} {software.Port}", indentLevel: 3, isBold: true);
                        foreach (var cve in software.Vulnerabilities?.Cves ?? [])
                        {
                            DrawText($"\u25E6 {cve.CveId}", indentLevel: 4);
                        }
                    }           
                }

                if (lastY == CurrentY)
                    DrawText($"{ResourceHelper.GetString("PdfNothingFoundForDevice")}", indentLevel: 2);

            }
            DrawText("");
        }

        Document.Save(filePath);

    }

    private void DrawText(
        string text, bool isTitle = false, bool isBold = false, bool useLastLine = false, int xOffset = 0, int indentLevel = 0)
    {
        if (!useLastLine && CurrentY + LineHeight > PageHeight - (PageMargin * 2))
        {
            Page = Document!.AddPage();
            Graphics?.Dispose();
            Graphics = XGraphics.FromPdfPage(Page);
            CurrentY = PageMargin;
        }

        if (useLastLine)
            CurrentY -= (isTitle ? LineHeight * 2 : LineHeight) + LineSpacing;
        
        Graphics?.DrawString(
            text,
            isTitle ? TitleFont : isBold ? TextFontBold : TextFont, 
            XBrushes.Black, 
            PageMargin + (PageMargin / 2 * indentLevel) + xOffset, 
            CurrentY += (isTitle ? LineHeight * 2 : LineHeight) + LineSpacing);
    }

    public void Dispose()
    {
        Graphics?.Dispose();
        Document?.Dispose();
    }

    private class AvaloniaFontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
            => new (bold ? "Inter-Bold.ttf" : "Inter-Regular.ttf");

        public byte[]? GetFont(string faceName)
        {
            using var stream = AssetLoader.Open(new ($"avares://Avalonia.Fonts.inter/Assets/{faceName}"));
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}