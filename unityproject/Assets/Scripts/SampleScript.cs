using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

/* Example usage of the framework :
 * We have three objects, red, green and blue. 
 * Depending on the height of these, we will change the color
 * of the cube.
 */
public class SampleScript : MonoBehaviour
{
    /* Assign any object that has the YolactServer script attached. */
    public GameObject yolactServerObject;

    /* Object we will use for this example. */
    public GameObject cube;

    /* Debugging */
    public GameObject debugCube1;
    public GameObject debugCube2;
    public GameObject debugCube3;

    /* Set these according to the camera settings. */
    public float camResWidth;
    public float camResHeight;

    private YoloServer _yolactServer;

    void Start()
    {
        _yolactServer = yolactServerObject.GetComponent<YoloServer>();
    }

    void Update()
    {
        /* Obtain the bboxesWrapper class */
        YoloServer.BboxesWrapper bw = _yolactServer.GetBboxesWrapper();
        float red = 0;
        float green = 0;
        float blue = 0;

        if (YoloServer.CheckBboxesWrapper(bw))
        {
            /* Find the indices of the first red, green and blue
             * classnames and set their height. 
             */
            bool redFound = false;
            bool greenFound = false;
            bool blueFound = false;

            for (int i = 0; i < bw.names.Length; i++)
            {
                if (!redFound && bw.names[i] == "red")
                {
                    redFound = true;

                    float x = (bw.bboxes[i * 4] + bw.bboxes[i * 4 + 2]) / 2;
                    float y = (bw.bboxes[i * 4 + 1] + bw.bboxes[i * 4 + 3]) / 2;

                    /* Since (0, 0) is top left, the lowest y value must correspond to the highest
                     * rgb value
                     */
                    red = Map(y, 0, camResHeight, 1, 0);

                    /* Debugging */
                    x = Map(x, 0, camResWidth, -8.4f, 8.4f);
                    y = Map(y, 0, camResHeight, 4.5f, -4.5f);
                    debugCube1.SetActive(true);
                    debugCube1.transform.position = new Vector3(x, y, 6);
                }
                else if (!greenFound && bw.names[i] == "green")
                {
                    greenFound = true;

                    float x = (bw.bboxes[i * 4] + bw.bboxes[i * 4 + 2]) / 2;
                    float y = (bw.bboxes[i * 4 + 1] + bw.bboxes[i * 4 + 3]) / 2;

                    green = Map(y, 0, camResHeight, 1, 0);

                    /* Debugging */
                    x = Map(x, 0, camResWidth, -8.4f, 8.4f);
                    y = Map(y, 0, camResHeight, 4.5f, -4.5f);
                    debugCube2.SetActive(true);
                    debugCube2.transform.position = new Vector3(x, y, 6);
                }
                else if (!blueFound && bw.names[i] == "blue")
                {
                    blueFound = true;

                    float x = (bw.bboxes[i * 4] + bw.bboxes[i * 4 + 2]) / 2;
                    float y = (bw.bboxes[i * 4 + 1] + bw.bboxes[i * 4 + 3]) / 2;

                    blue = Map(y, 0, camResHeight, 1, 0);

                    /* Debugging */
                    x = Map(x, 0, camResWidth, -8.4f, 8.4f);
                    y = Map(y, 0, camResHeight, 4.5f, -4.5f);
                    debugCube3.SetActive(true);
                    debugCube3.transform.position = new Vector3(x, y, 6);
                }
            }

            /* Debugging */
            if (!redFound)
                debugCube1.SetActive(false);
            if (!greenFound)
                debugCube2.SetActive(false);
            if (!blueFound)
                debugCube3.SetActive(false);
        }
        
        cube.GetComponent<Renderer>().material.color = new Color(red, green, blue);
    }

    private float Map(float x, float in_min, float in_max, float out_min, float out_max)
    {
        return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }
}
