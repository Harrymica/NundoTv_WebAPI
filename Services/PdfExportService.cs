using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NundoTv_WebAPI.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace NundoTv_WebAPI.Services
{
    public interface IPdfExportService
    {
        byte[] GenerateStreamLinksPdf(IEnumerable<StreamLink> streamLinks, string title = "NundoTV Stream Links Directory");
    }

    public class PdfExportService : IPdfExportService
    {
        public byte[] GenerateStreamLinksPdf(IEnumerable<StreamLink> streamLinks, string title = "NundoTV Stream Links Directory")
        {
            var document = new PdfDocument();
            document.Info.Title = title;
            document.Info.Author = "NundoTV API";
            document.Info.Subject = "Export of Database Stream Links";
            document.Info.CreationDate = DateTime.UtcNow;

            var list = streamLinks?.ToList() ?? new List<StreamLink>();

            // Fonts
            var fontTitle = new XFont("Helvetica", 18, XFontStyle.Bold);
            var fontSubtitle = new XFont("Helvetica", 10, XFontStyle.Italic);
            var fontCategoryHeader = new XFont("Helvetica", 12, XFontStyle.Bold);
            var fontTableHeader = new XFont("Helvetica", 9, XFontStyle.Bold);
            var fontTableCell = new XFont("Helvetica", 8, XFontStyle.Regular);
            var fontFooter = new XFont("Helvetica", 8, XFontStyle.Italic);

            // Colors
            var colorHeaderBg = XColor.FromArgb(20, 24, 33);
            var colorTitleText = XColor.FromArgb(255, 255, 255);
            var colorAccent = XColor.FromArgb(229, 9, 20); // NundoTV Red
            var colorCatHeaderBg = XColor.FromArgb(240, 243, 246);
            var colorTableHeadBg = XColor.FromArgb(40, 48, 60);
            var colorRowAlt = XColor.FromArgb(248, 249, 250);
            var colorBorder = XColor.FromArgb(220, 224, 230);
            var colorOnline = XColor.FromArgb(46, 125, 50);
            var colorOffline = XColor.FromArgb(198, 40, 40);

            // Page dimensions
            var page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            page.Orientation = PdfSharpCore.PageOrientation.Portrait;

            var gfx = XGraphics.FromPdfPage(page);

            double marginX = 25;
            double marginY = 25;
            double pageWidth = page.Width.Point - (marginX * 2);
            double pageHeight = page.Height.Point;
            double currentY = marginY;

            void DrawPageHeader()
            {
                // Top Header Bar
                gfx.DrawRectangle(new XSolidBrush(colorHeaderBg), marginX, currentY, pageWidth, 50);
                gfx.DrawString(title, fontTitle, new XSolidBrush(colorTitleText), new XRect(marginX + 15, currentY + 8, pageWidth - 30, 24), XStringFormats.TopLeft);
                gfx.DrawString($"Exported on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  |  Total Items: {list.Count}", fontSubtitle, new XSolidBrush(XColor.FromArgb(180, 190, 200)), new XRect(marginX + 15, currentY + 32, pageWidth - 30, 14), XStringFormats.TopLeft);
                
                // Accent Line
                gfx.DrawRectangle(new XSolidBrush(colorAccent), marginX, currentY + 50, pageWidth, 3);
                currentY += 62;
            }

            DrawPageHeader();

            // Group by category
            var grouped = list.GroupBy(s => string.IsNullOrWhiteSpace(s.Category) ? "Uncategorized" : s.Category)
                              .OrderBy(g => g.Key);

            double colIdWidth = 35;
            double colSiteWidth = 110;
            double colTypeWidth = 65;
            double colStatusWidth = 55;
            double colUrlWidth = pageWidth - (colIdWidth + colSiteWidth + colTypeWidth + colStatusWidth);

            void DrawTableHeader()
            {
                gfx.DrawRectangle(new XSolidBrush(colorTableHeadBg), marginX, currentY, pageWidth, 20);

                double curX = marginX;
                void CellHead(string txt, double w, XStringFormat fmt)
                {
                    gfx.DrawString(txt, fontTableHeader, XBrushes.White, new XRect(curX + 4, currentY + 3, w - 8, 14), fmt);
                    curX += w;
                }

                CellHead("ID", colIdWidth, XStringFormats.TopLeft);
                CellHead("Site / Name", colSiteWidth, XStringFormats.TopLeft);
                CellHead("Type", colTypeWidth, XStringFormats.TopLeft);
                CellHead("Status", colStatusWidth, XStringFormats.Center);
                CellHead("Target URL", colUrlWidth, XStringFormats.TopLeft);

                currentY += 20;
            }

            int itemCounter = 0;

            foreach (var group in grouped)
            {
                // Check page space for category banner
                if (currentY + 60 > pageHeight - 40)
                {
                    gfx?.Dispose();
                    page = document.AddPage();
                    page.Size = PdfSharpCore.PageSize.A4;
                    gfx = XGraphics.FromPdfPage(page);
                    currentY = marginY;
                    DrawPageHeader();
                }

                // Category Section Header
                gfx.DrawRectangle(new XSolidBrush(colorCatHeaderBg), marginX, currentY, pageWidth, 22);
                gfx.DrawRectangle(new XPen(colorBorder, 0.5), marginX, currentY, pageWidth, 22);
                gfx.DrawString($"Category: {group.Key.ToUpper()} ({group.Count()})", fontCategoryHeader, new XSolidBrush(colorHeaderBg), new XRect(marginX + 8, currentY + 4, pageWidth - 16, 16), XStringFormats.TopLeft);
                currentY += 24;

                DrawTableHeader();

                bool isAlt = false;

                foreach (var item in group)
                {
                    itemCounter++;

                    if (currentY + 22 > pageHeight - 40)
                    {
                        gfx?.Dispose();
                        page = document.AddPage();
                        page.Size = PdfSharpCore.PageSize.A4;
                        gfx = XGraphics.FromPdfPage(page);
                        currentY = marginY;
                        DrawPageHeader();
                        DrawTableHeader();
                    }

                    var rowBg = isAlt ? colorRowAlt : XColors.White;
                    isAlt = !isAlt;

                    gfx.DrawRectangle(new XSolidBrush(rowBg), marginX, currentY, pageWidth, 18);
                    gfx.DrawRectangle(new XPen(colorBorder, 0.5), marginX, currentY, pageWidth, 18);

                    double curX = marginX;

                    // ID
                    gfx.DrawString(item.Id.ToString(), fontTableCell, XBrushes.Black, new XRect(curX + 4, currentY + 3, colIdWidth - 8, 12), XStringFormats.TopLeft);
                    curX += colIdWidth;

                    // SiteName
                    string siteStr = item.SiteName.Length > 22 ? item.SiteName.Substring(0, 20) + ".." : item.SiteName;
                    gfx.DrawString(siteStr, fontTableCell, XBrushes.Black, new XRect(curX + 4, currentY + 3, colSiteWidth - 8, 12), XStringFormats.TopLeft);
                    curX += colSiteWidth;

                    // StreamType
                    string typeStr = item.StreamType ?? "Direct";
                    gfx.DrawString(typeStr, fontTableCell, XBrushes.DarkGray, new XRect(curX + 4, currentY + 3, colTypeWidth - 8, 12), XStringFormats.TopLeft);
                    curX += colTypeWidth;

                    // Status
                    string statusStr = item.IsOnline ? "ONLINE" : "OFFLINE";
                    var statusBrush = new XSolidBrush(item.IsOnline ? colorOnline : colorOffline);
                    gfx.DrawString(statusStr, fontTableCell, statusBrush, new XRect(curX + 2, currentY + 3, colStatusWidth - 4, 12), XStringFormats.TopCenter);
                    curX += colStatusWidth;

                    // TargetUrl
                    string urlStr = item.TargetUrl.Length > 65 ? item.TargetUrl.Substring(0, 62) + "..." : item.TargetUrl;
                    gfx.DrawString(urlStr, fontTableCell, XBrushes.Navy, new XRect(curX + 4, currentY + 3, colUrlWidth - 8, 12), XStringFormats.TopLeft);

                    currentY += 18;
                }

                currentY += 12; // Spacing after group
            }

            // Dispose main content graphics handle before footer loop
            gfx?.Dispose();

            // Draw page footers
            for (int i = 0; i < document.PageCount; i++)
            {
                var p = document.Pages[i];
                using (var g = XGraphics.FromPdfPage(p))
                {
                    string footerText = $"NundoTV API  •  Page {i + 1} of {document.PageCount}";
                    g.DrawString(footerText, fontFooter, XBrushes.Gray, new XRect(marginX, p.Height.Point - 20, pageWidth, 12), XStringFormats.TopCenter);
                }
            }

            using var ms = new MemoryStream();
            document.Save(ms, false);
            return ms.ToArray();
        }
    }
}
