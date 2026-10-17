using System;
using System.IO;
/*
Copyright (c) 2022 Sergii Kudlai

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */
namespace IggyViewer {




/**
 * Decodes linear BC7 blocks to straight-alpha ARGB pixels, including all eight
 * modes, partitions, anchor indices, P-bits and component rotations.
 *
 * Partition tables and endpoint/index decoding adapted from bcdec by Sergii
 * Kudlai: https://github.com/iOrange/bcdec (MIT license above). Format:
 * https://learn.microsoft.com/en-us/windows/win32/direct3d11/bc7-format
 */
internal sealed class Bc7Decoder {

    private static readonly int[] COLOR_BITS = {4, 6, 5, 7, 5, 7, 7, 5};
    private static readonly int[] ALPHA_BITS = {0, 0, 0, 0, 6, 8, 7, 5};
    private static readonly int[][] WEIGHTS = new int[][] {
new int[] {0, 21, 43, 64},
new int[] {0, 9, 18, 27, 37, 46, 55, 64},
new int[] {0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64}
};

    private static readonly int[][][] PARTITIONS = new int[][][] {
new int[][] {
new int[] {128, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 129},
new int[] {128, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 129},
new int[] {128, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 129},
new int[] {128, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 129},
new int[] {128, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 129},
new int[] {128, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 129},
new int[] {128, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 129},
new int[] {128, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 129},
new int[] {128, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 129},
new int[] {128, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 129},
new int[] {128, 1, 129, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 129, 0, 0, 0, 1, 1, 1, 0},
new int[] {128, 1, 129, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0},
new int[] {128, 0, 129, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0},
new int[] {128, 0, 0, 0, 1, 0, 0, 0, 129, 1, 0, 0, 1, 1, 1, 0},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 129, 0, 0, 0, 1, 1, 0, 0},
new int[] {128, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 129},
new int[] {128, 0, 129, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0},
new int[] {128, 0, 0, 0, 1, 0, 0, 0, 129, 0, 0, 0, 1, 1, 0, 0},
new int[] {128, 1, 129, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0},
new int[] {128, 0, 129, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0},
new int[] {128, 0, 0, 1, 0, 1, 1, 1, 129, 1, 1, 0, 1, 0, 0, 0},
new int[] {128, 0, 0, 0, 1, 1, 1, 1, 129, 1, 1, 1, 0, 0, 0, 0},
new int[] {128, 1, 129, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0},
new int[] {128, 0, 129, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0},
new int[] {128, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 129},
new int[] {128, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 129},
new int[] {128, 1, 0, 1, 1, 0, 129, 0, 0, 1, 0, 1, 1, 0, 1, 0},
new int[] {128, 0, 1, 1, 0, 0, 1, 1, 129, 1, 0, 0, 1, 1, 0, 0},
new int[] {128, 0, 129, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0},
new int[] {128, 1, 0, 1, 0, 1, 0, 1, 129, 0, 1, 0, 1, 0, 1, 0},
new int[] {128, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 129},
new int[] {128, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 129},
new int[] {128, 1, 129, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0},
new int[] {128, 0, 0, 1, 0, 0, 1, 1, 129, 1, 0, 0, 1, 0, 0, 0},
new int[] {128, 0, 129, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0},
new int[] {128, 0, 129, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0, 0},
new int[] {128, 1, 129, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0},
new int[] {128, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1, 129},
new int[] {128, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 129},
new int[] {128, 0, 0, 0, 0, 1, 129, 0, 0, 1, 1, 0, 0, 0, 0, 0},
new int[] {128, 1, 0, 0, 1, 1, 129, 0, 0, 1, 0, 0, 0, 0, 0, 0},
new int[] {128, 0, 129, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0},
new int[] {128, 0, 0, 0, 0, 0, 129, 0, 0, 1, 1, 1, 0, 0, 1, 0},
new int[] {128, 0, 0, 0, 0, 1, 0, 0, 129, 1, 1, 0, 0, 1, 0, 0},
new int[] {128, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 129},
new int[] {128, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 129},
new int[] {128, 1, 129, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0},
new int[] {128, 0, 129, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0},
new int[] {128, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 129},
new int[] {128, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 129},
new int[] {128, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 129},
new int[] {128, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 129},
new int[] {128, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 129},
new int[] {128, 0, 129, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0},
new int[] {128, 0, 129, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0},
new int[] {128, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 129}
},
new int[][] {
new int[] {128, 0, 1, 129, 0, 0, 1, 1, 0, 2, 2, 1, 2, 2, 2, 130},
new int[] {128, 0, 0, 129, 0, 0, 1, 1, 130, 2, 1, 1, 2, 2, 2, 1},
new int[] {128, 0, 0, 0, 2, 0, 0, 1, 130, 2, 1, 1, 2, 2, 1, 129},
new int[] {128, 2, 2, 130, 0, 0, 2, 2, 0, 0, 1, 1, 0, 1, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 129, 1, 2, 2, 1, 1, 2, 130},
new int[] {128, 0, 1, 129, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 2, 130},
new int[] {128, 0, 2, 130, 0, 0, 2, 2, 1, 1, 1, 1, 1, 1, 1, 129},
new int[] {128, 0, 1, 1, 0, 0, 1, 1, 130, 2, 1, 1, 2, 2, 1, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 129, 1, 1, 1, 2, 2, 2, 130},
new int[] {128, 0, 0, 0, 1, 1, 1, 1, 129, 1, 1, 1, 2, 2, 2, 130},
new int[] {128, 0, 0, 0, 1, 1, 129, 1, 2, 2, 2, 2, 2, 2, 2, 130},
new int[] {128, 0, 1, 2, 0, 0, 129, 2, 0, 0, 1, 2, 0, 0, 1, 130},
new int[] {128, 1, 1, 2, 0, 1, 129, 2, 0, 1, 1, 2, 0, 1, 1, 130},
new int[] {128, 1, 2, 2, 0, 129, 2, 2, 0, 1, 2, 2, 0, 1, 2, 130},
new int[] {128, 0, 1, 129, 0, 1, 1, 2, 1, 1, 2, 2, 1, 2, 2, 130},
new int[] {128, 0, 1, 129, 2, 0, 0, 1, 130, 2, 0, 0, 2, 2, 2, 0},
new int[] {128, 0, 0, 129, 0, 0, 1, 1, 0, 1, 1, 2, 1, 1, 2, 130},
new int[] {128, 1, 1, 129, 0, 0, 1, 1, 130, 0, 0, 1, 2, 2, 0, 0},
new int[] {128, 0, 0, 0, 1, 1, 2, 2, 129, 1, 2, 2, 1, 1, 2, 130},
new int[] {128, 0, 2, 130, 0, 0, 2, 2, 0, 0, 2, 2, 1, 1, 1, 129},
new int[] {128, 1, 1, 129, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 130},
new int[] {128, 0, 0, 129, 0, 0, 0, 1, 130, 2, 2, 1, 2, 2, 2, 1},
new int[] {128, 0, 0, 0, 0, 0, 129, 1, 0, 1, 2, 2, 0, 1, 2, 130},
new int[] {128, 0, 0, 0, 1, 1, 0, 0, 130, 2, 129, 0, 2, 2, 1, 0},
new int[] {128, 1, 2, 130, 0, 129, 2, 2, 0, 0, 1, 1, 0, 0, 0, 0},
new int[] {128, 0, 1, 2, 0, 0, 1, 2, 129, 1, 2, 2, 2, 2, 2, 130},
new int[] {128, 1, 1, 0, 1, 2, 130, 1, 129, 2, 2, 1, 0, 1, 1, 0},
new int[] {128, 0, 0, 0, 0, 1, 129, 0, 1, 2, 130, 1, 1, 2, 2, 1},
new int[] {128, 0, 2, 2, 1, 1, 0, 2, 129, 1, 0, 2, 0, 0, 2, 130},
new int[] {128, 1, 1, 0, 0, 129, 1, 0, 2, 0, 0, 2, 2, 2, 2, 130},
new int[] {128, 0, 1, 1, 0, 1, 2, 2, 0, 1, 130, 2, 0, 0, 1, 129},
new int[] {128, 0, 0, 0, 2, 0, 0, 0, 130, 2, 1, 1, 2, 2, 2, 129},
new int[] {128, 0, 0, 0, 0, 0, 0, 2, 129, 1, 2, 2, 1, 2, 2, 130},
new int[] {128, 2, 2, 130, 0, 0, 2, 2, 0, 0, 1, 2, 0, 0, 1, 129},
new int[] {128, 0, 1, 129, 0, 0, 1, 2, 0, 0, 2, 2, 0, 2, 2, 130},
new int[] {128, 1, 2, 0, 0, 129, 2, 0, 0, 1, 130, 0, 0, 1, 2, 0},
new int[] {128, 0, 0, 0, 1, 1, 129, 1, 2, 2, 130, 2, 0, 0, 0, 0},
new int[] {128, 1, 2, 0, 1, 2, 0, 1, 130, 0, 129, 2, 0, 1, 2, 0},
new int[] {128, 1, 2, 0, 2, 0, 1, 2, 129, 130, 0, 1, 0, 1, 2, 0},
new int[] {128, 0, 1, 1, 2, 2, 0, 0, 1, 1, 130, 2, 0, 0, 1, 129},
new int[] {128, 0, 1, 1, 1, 1, 130, 2, 2, 2, 0, 0, 0, 0, 1, 129},
new int[] {128, 1, 0, 129, 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 130},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 130, 1, 2, 1, 2, 1, 2, 129},
new int[] {128, 0, 2, 2, 1, 129, 2, 2, 0, 0, 2, 2, 1, 1, 2, 130},
new int[] {128, 0, 2, 130, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 1, 129},
new int[] {128, 2, 2, 0, 1, 2, 130, 1, 0, 2, 2, 0, 1, 2, 2, 129},
new int[] {128, 1, 0, 1, 2, 2, 130, 2, 2, 2, 2, 2, 0, 1, 0, 129},
new int[] {128, 0, 0, 0, 2, 1, 2, 1, 130, 1, 2, 1, 2, 1, 2, 129},
new int[] {128, 1, 0, 129, 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 130},
new int[] {128, 2, 2, 130, 0, 1, 1, 1, 0, 2, 2, 2, 0, 1, 1, 129},
new int[] {128, 0, 0, 2, 1, 129, 1, 2, 0, 0, 0, 2, 1, 1, 1, 130},
new int[] {128, 0, 0, 0, 2, 129, 1, 2, 2, 1, 1, 2, 2, 1, 1, 130},
new int[] {128, 2, 2, 2, 0, 129, 1, 1, 0, 1, 1, 1, 0, 2, 2, 130},
new int[] {128, 0, 0, 2, 1, 1, 1, 2, 129, 1, 1, 2, 0, 0, 0, 130},
new int[] {128, 1, 1, 0, 0, 129, 1, 0, 0, 1, 1, 0, 2, 2, 2, 130},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 2, 1, 129, 2, 2, 1, 1, 130},
new int[] {128, 1, 1, 0, 0, 129, 1, 0, 2, 2, 2, 2, 2, 2, 2, 130},
new int[] {128, 0, 2, 2, 0, 0, 1, 1, 0, 0, 129, 1, 0, 0, 2, 130},
new int[] {128, 0, 2, 2, 1, 1, 2, 2, 129, 1, 2, 2, 0, 0, 2, 130},
new int[] {128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 129, 1, 130},
new int[] {128, 0, 0, 130, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 129},
new int[] {128, 2, 2, 2, 1, 2, 2, 2, 0, 2, 2, 2, 129, 2, 2, 130},
new int[] {128, 1, 0, 129, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 130},
new int[] {128, 1, 1, 129, 2, 0, 1, 1, 130, 2, 0, 1, 2, 2, 2, 0}
}
};

    private Bc7Decoder() {
    }

    /**
     * Decodes a row-major array of 16-byte, 4x4 blocks. Partial edge blocks are
     * cropped to the requested dimensions. Reserved modes yield transparent
     * black.
     *
     * @param width image width in pixels
     * @param height image height in pixels
     * @param data compressed blocks
     * @return row-major ARGB pixels
     * @throws InvalidDataException if dimensions or compressed data length are invalid
     */
    public static int[] decode(int width, int height, byte[] data) {
        long count = (long) width * height;
        long blocks = ((width + 3L) / 4) * ((height + 3L) / 4);
        if (width <= 0 || height <= 0 || count > int.MaxValue
                || blocks * 16 > data.Length) {
            throw new InvalidDataException("Invalid BC7 dimensions or truncated blocks");
        }
        int[] pixels = new int[(int) count];
        int[] block = new int[16];
        int[][] endpoints = new int[6][]; for (int i = 0; i < 6; i++) endpoints[i] = new int[4];
        int[] indices = new int[16];
        Bits bits = new Bits(data);
        for (int y = 0; y < height; y += 4) {
            for (int x = 0; x < width; x += 4) {
                decodeBlock(bits, endpoints, indices, block);
                bits.nextBlock();
                for (int row = 0; row < 4 && y + row < height; row++) {
                    Array.Copy(block, row * 4, pixels, (y + row) * width + x, Math.Min(4, width - x));
                }
            }
        }
        return pixels;
    }

    private static void decodeBlock(Bits bits, int[][] endpoints, int[] indices, int[] pixels) {
        int mode = 0;
        while (mode < 8 && bits.read(1) == 0) {
            mode++;
        }
        if (mode == 8) {
            Array.Clear(pixels, 0, pixels.Length);
            return;
        }
        int subsets = mode == 0 || mode == 2 ? 3 : (mode == 1 || mode == 3 || mode == 7 ? 2 : 1);
        int partition = subsets == 1 ? 0 : bits.read(mode == 0 ? 4 : 6);
        int rotation = mode == 4 || mode == 5 ? bits.read(2) : 0;
        int selection = mode == 4 ? bits.read(1) : 0;
        int numEndpoints = subsets * 2;
        for (int channel = 0; channel < 4; channel++) {
            int precision = channel == 3 ? ALPHA_BITS[mode] : COLOR_BITS[mode];
            for (int endpoint = 0; endpoint < numEndpoints; endpoint++) {
                endpoints[endpoint][channel] = precision == 0 ? 0 : bits.read(precision);
            }
        }

        bool hasPBits = (0xcb & (1 << mode)) != 0;
        if (hasPBits) {
            int pbit = 0;
            for (int endpoint = 0; endpoint < numEndpoints; endpoint++) {
                // Mode 1 shares one P-bit between the two endpoints of a subset.
                if (mode != 1 || (endpoint & 1) == 0) {
                    pbit = bits.read(1);
                }
                for (int channel = 0; channel < 4; channel++) {
                    endpoints[endpoint][channel] = (endpoints[endpoint][channel] << 1) | pbit;
                }
            }
        }
        for (int endpoint = 0; endpoint < numEndpoints; endpoint++) {
            for (int channel = 0; channel < 4; channel++) {
                if (channel == 3 && ALPHA_BITS[mode] == 0) {
                    endpoints[endpoint][channel] = 255;
                } else {
                    int precision = (channel == 3 ? ALPHA_BITS[mode] : COLOR_BITS[mode]) + (hasPBits ? 1 : 0);
                    int value = endpoints[endpoint][channel] << (8 - precision);
                    endpoints[endpoint][channel] = value | (value >> precision);
                }
            }
        }

        int indexBits = mode == 0 || mode == 1 ? 3 : (mode == 6 ? 4 : 2);
        int secondaryBits = mode == 4 ? 3 : (mode == 5 ? 2 : 0);
        for (int pixel = 0; pixel < 16; pixel++) {
            int subset = Partition(subsets, partition, pixel);
            indices[pixel] = bits.read(indexBits - ((subset & 128) != 0 ? 1 : 0));
        }
        for (int pixel = 0; pixel < 16; pixel++) {
            int subset = Partition(subsets, partition, pixel) & 3;
            int primaryWeight = WEIGHTS[indexBits - 2][indices[pixel]];
            int secondaryWeight = secondaryBits == 0 ? primaryWeight
                    : WEIGHTS[secondaryBits - 2][bits.read(secondaryBits - (pixel == 0 ? 1 : 0))];
            int colorWeight = selection == 0 ? primaryWeight : secondaryWeight;
            int alphaWeight = selection == 0 ? secondaryWeight : primaryWeight;
            int[] start = endpoints[subset * 2];
            int[] end = endpoints[subset * 2 + 1];
            int r = interpolate(start[0], end[0], colorWeight);
            int g = interpolate(start[1], end[1], colorWeight);
            int b = interpolate(start[2], end[2], colorWeight);
            int a = interpolate(start[3], end[3], alphaWeight);
            int temp;
            switch (rotation) {
                case 1:
                    temp = r;
                    r = a;
                    a = temp;
                    break;
                case 2:
                    temp = g;
                    g = a;
                    a = temp;
                    break;
                case 3:
                    temp = b;
                    b = a;
                    a = temp;
                    break;
                default:
                    break;
            }
            pixels[pixel] = a << 24 | r << 16 | g << 8 | b;
        }
    }

    private static int Partition(int subsets, int partition, int pixel) {
        return subsets == 1 ? (pixel == 0 ? 128 : 0) : PARTITIONS[subsets - 2][partition][pixel];
    }

    private static int interpolate(int start, int end, int weight) {
        return ((64 - weight) * start + weight * end + 32) >> 6;
    }

    private sealed class Bits {

        private readonly byte[] data;
        private int offset;
        private int position;

        public Bits(byte[] data) {
            this.data = data;
        }

        public int read(int count) {
            int index = offset + (position >> 3);
            int shift = position & 7;
            int value = (data[index] & 255) >> shift;
            if (shift + count > 8) {
                value |= (data[index + 1] & 255) << (8 - shift);
            }
            position += count;
            return value & ((1 << count) - 1);
        }

        public void nextBlock() {
            offset += 16;
            position = 0;
        }
    }
}

}

