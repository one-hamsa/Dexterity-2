using System;
using System.Runtime.InteropServices;

public static class Graphviz
{
    public const string LIB_GVC = "gvc";
    public const string LIB_GRAPH = "cgraph";
    public const int SUCCESS = 0;

    /// <summary> 
    /// Creates a new Graphviz context. 
    /// </summary> 
    [DllImport(LIB_GVC, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr gvContext();

    /// <summary> 
    /// Reads a graph from a string. 
    /// </summary> 
    [DllImport(LIB_GRAPH, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr agmemread(string data);

    /// <summary> 
    /// Renders a graph in memory. 
    /// </summary> 
    [DllImport(LIB_GVC, CallingConvention = CallingConvention.Cdecl)]
    public static extern int gvRenderData(IntPtr gvc, IntPtr g,
        string format, out IntPtr result, out int length);

    /// <summary> 
    /// Applies a layout to a graph using the given engine. 
    /// </summary> 
    [DllImport(LIB_GVC, CallingConvention = CallingConvention.Cdecl)]
    public static extern int gvLayout(IntPtr gvc, IntPtr g, string engine);

    /// <summary> 
    /// Releases the resources used by a layout. 
    /// </summary> 
    [DllImport(LIB_GVC, CallingConvention = CallingConvention.Cdecl)]
    public static extern int gvFreeLayout(IntPtr gvc, IntPtr g);

    /// <summary> 
    /// Releases a context's resources. 
    /// </summary> 
    [DllImport(LIB_GVC, CallingConvention = CallingConvention.Cdecl)]
    public static extern int gvFreeContext(IntPtr gvc);

    /// <summary> 
    /// Releases the resources used by a graph. 
    /// </summary> 
    [DllImport(LIB_GRAPH, CallingConvention = CallingConvention.Cdecl)]
    public static extern int agclose(IntPtr g);

    public static byte[] RenderImage(string source, string layout, string format)
    {
        // Create a Graphviz context 
        IntPtr gvc = gvContext();
        if (gvc == IntPtr.Zero)
            throw new Exception("Failed to create Graphviz context.");

        // Load the DOT data into a graph 
        IntPtr g = agmemread(source);
        if (g == IntPtr.Zero)
            throw new Exception("Failed to create graph from source. Check for syntax errors.");

        // Apply a layout 
        if (gvLayout(gvc, g, layout) != SUCCESS)
            throw new Exception("Layout failed.");

        IntPtr result;
        int length;

        // Render the graph 
        if (gvRenderData(gvc, g, format, out result, out length) != SUCCESS)
            throw new Exception("Render failed.");

        // Create an array to hold the rendered graph
        byte[] bytes = new byte[length];

        // Copy the image from the IntPtr 
        Marshal.Copy(result, bytes, 0, length);

        // Free up the resources 
        gvFreeLayout(gvc, g);
        agclose(g);
        gvFreeContext(gvc);

        return bytes;

    }
}