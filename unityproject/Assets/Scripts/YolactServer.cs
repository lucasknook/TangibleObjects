using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEngine;

/* https://thoughtbot.com/blog/using-httplistener-to-build-a-http-server-in-csharp */
public class YoloServer : MonoBehaviour
{
    public int port;

    private HttpListener _listener;
    private BboxesWrapper _bboxeswrapper = new();

    Thread thread;

    public void Start()
    {
        _listener = new HttpListener();
        string prefix = "http://*:" + port.ToString() + "/";
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        Debug.Log("Started server on " + prefix);

        /* Start a thread to prevent blocking the main thread */
        ThreadStart ts = new(Receive);
        thread = new Thread(ts);
        thread.Start();
    }

    public void Stop()
    {
        _listener.Stop();
    }

    private void Receive()
    {
        _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
    }

    private void ListenerCallback(IAsyncResult result)
    {
        if (_listener.IsListening)
        {
            HttpListenerContext context = _listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            string bboxes_json = "";
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                bboxes_json = reader.ReadToEnd();
            }
            StoreBboxes(bboxes_json);

            response.StatusCode = (int) HttpStatusCode.OK;
            response.ContentType = "text/plain";
            response.OutputStream.Write(new byte[] {}, 0, 0);
            response.OutputStream.Close();
            Receive();
        }
    }

    private void StoreBboxes(string bboxes_json)
    {
        JsonUtility.FromJsonOverwrite(bboxes_json, _bboxeswrapper);
    }
    static public bool CheckBboxesWrapper(BboxesWrapper bw)
    {
        if (bw == null)
        {
            Debug.LogError("BboxesWrapper is null.");
            return false;
        }

        if (bw.bboxes == null ||
            bw.masks == null ||
            bw.mask_lengths == null ||
            bw.angles == null ||
            bw.names == null)
        {
            Debug.Log("BboxesWrapper contains no data.");
            return false;
        }

        if (bw.bboxes.Length >= 4 && bw.angles.Length >= 1 && bw.names.Length >= 1 &&
            bw.bboxes.Length == 4 * bw.mask_lengths.Length &&
            bw.mask_lengths.Length == bw.angles.Length &&
            bw.angles.Length == bw.names.Length)
            /* Check if the length of masks equals the sum of mask_lengths */
        {
            return true;
        }

        Debug.Log("BboxesWrapper does not contain the correct format.");
        return false;
     }

    /* Class to save bboxes in */
    [Serializable]
    public class BboxesWrapper
    {
        public int max_width;
        public int max_height;
        public int[] bboxes;
        public int[] masks;
        public int[] mask_lengths;
        public int[] angles;
        public string[] names;
    }

    public BboxesWrapper GetBboxesWrapper()
    {
        return _bboxeswrapper;
    }
}