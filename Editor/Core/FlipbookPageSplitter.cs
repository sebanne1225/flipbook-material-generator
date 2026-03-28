using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sebanne.FlipbookMaterialGenerator.Editor
{
    internal sealed class FlipbookPageInfo
    {
        internal int PageIndex { get; }
        internal Texture2D[] Frames { get; }

        internal FlipbookPageInfo(int pageIndex, Texture2D[] frames)
        {
            PageIndex = pageIndex;
            Frames = frames;
        }
    }

    internal sealed class FlipbookPageSplitResult
    {
        internal int PageCount { get; }
        internal int FramesPerPage { get; }
        internal int TotalFrames { get; }
        internal int EmptySlots { get; }
        internal List<FlipbookPageInfo> Pages { get; }

        internal FlipbookPageSplitResult(
            int pageCount, int framesPerPage, int totalFrames, int emptySlots,
            List<FlipbookPageInfo> pages)
        {
            PageCount = pageCount;
            FramesPerPage = framesPerPage;
            TotalFrames = totalFrames;
            EmptySlots = emptySlots;
            Pages = pages;
        }
    }

    internal static class FlipbookPageSplitter
    {
        private const int DefaultFrameSize = 256;

        internal static int CalculateFramesPerPage(int maxSheetSize, int frameSize = DefaultFrameSize)
        {
            var maxCols = maxSheetSize / frameSize;
            var maxRows = maxSheetSize / frameSize;
            return maxCols * maxRows;
        }

        internal static FlipbookPageSplitResult Split(Texture2D[] allFrames, int framesPerPage)
        {
            if (allFrames == null || allFrames.Length == 0)
            {
                FlipbookGeneratorLog.Error("No frames provided to FlipbookPageSplitter.");
                return null;
            }

            var pages = new List<FlipbookPageInfo>();
            var totalFrames = allFrames.Length;

            for (var offset = 0; offset < totalFrames; offset += framesPerPage)
            {
                var count = Math.Min(framesPerPage, totalFrames - offset);
                var pageFrames = new Texture2D[count];
                Array.Copy(allFrames, offset, pageFrames, 0, count);
                pages.Add(new FlipbookPageInfo(pages.Count, pageFrames));
            }

            var lastPageFrames = pages[pages.Count - 1].Frames.Length;
            var emptySlots = framesPerPage - lastPageFrames;

            FlipbookGeneratorLog.Info(
                $"Split {totalFrames} frames into {pages.Count} page(s), " +
                $"{framesPerPage} frames/page, {emptySlots} empty slot(s) on last page.");

            return new FlipbookPageSplitResult(
                pages.Count, framesPerPage, totalFrames, emptySlots, pages);
        }
    }
}
