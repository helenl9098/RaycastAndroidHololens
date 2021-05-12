using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum BuildType { HoloLens = 0, Android = 1};
public class RayCastController : MonoBehaviour
{
    string objName;
    public bool rayCastOn;
    public BuildType m_buildType = 0;
    public NetworkManagerHUD networkmanager;
    public GameObject target;

    public struct RayCastAndroidMessage : NetworkMessage
    {
        public string imageTargetName;
        public Vector3 cameraPosition;
        public Vector3 rayDirection;
    }

    public struct RayCastHololensMessage : NetworkMessage
    {
        public Vector3 targetPosition;
        public string debugMsg;
    }

    // Start is called before the first frame update
    void Start()
    {
        rayCastOn = true;

        if (m_buildType == 0)
        {
            SetupClient();
            networkmanager.manager.StartClient();
        }
        else {
            SetupServer();
            networkmanager.manager.StartServer();
        }
    }

    // Input: the clicked or touched screen position
    void rayCast(Vector3 position)
    {
        Debug.Log("Debug: touched or clicked screen");

        // find the ARCamera
        GameObject camObject = GameObject.Find("ARCamera");
        Camera cam = camObject.GetComponent<Camera>();

        // Calculate the ray
        Ray ray = cam.ScreenPointToRay(position); // this resulting ray is in WORLDSPACE (which is good!)

        // Need to send 3 things to Hololens:
        // 1) Which image Target is this relative to
        // 2) Camera position w.r.t the image target position
        // 3) Ray direction

        Vector3 originPosition = camObject.transform.position - this.gameObject.transform.position;
        //Debug.Log("Image Target Name: " + this.gameObject.name);
        //Debug.Log("Camera's Position w.r.t Image Target: " + originPosition.ToString());
        //Debug.Log("Ray Direction in WorldSpace: " + ray.direction.ToString());

        RayCastAndroidMessage msg = new RayCastAndroidMessage()
        {
            imageTargetName = this.gameObject.name,
            cameraPosition = originPosition,
            rayDirection = ray.direction
        };

        NetworkServer.SendToAll(msg);

        /*
        // on the Hololens, using the image target's name, we get the image target's world position
        // then, we can get the camera's world position, 
        // and finally, we shoot a ray from that camera's position in the given direction
        // how we cast the ray looks like the below code:
        RaycastHit Hit;
        if (Physics.Raycast(ray, out Hit))
        {
            objName = Hit.transform.name;
        }*/

    }

    // Update is called once per frame
    void Update()
    {
        if (rayCastOn && m_buildType != 0)
        {
            // if the screen is touched (MOBILE SIDE)
            if (Input.touchCount > 0)
            {
                foreach (Touch touch in Input.touches)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        rayCast(Input.GetTouch(0).position);
                    }
                }
            }

            // if the screen is clicked (TESTING/PC SIDE)
            if (Input.GetMouseButtonDown(0))
            {
                rayCast(Input.mousePosition);
            }
        }
    }

    public void SetupServer()
    {
        NetworkServer.RegisterHandler<RayCastHololensMessage>(OnRayCastReceive);
    }

    public void OnRayCastReceive(RayCastHololensMessage msg)
    {
        // show the target in the right place here
        target.SetActive(true);
        target.transform.position = msg.targetPosition + this.gameObject.transform.position;
    }

    public void SetupClient()
    {
        NetworkClient.RegisterHandler<RayCastAndroidMessage>(OnRayCastSend);
    }

    public void OnRayCastSend(RayCastAndroidMessage msg)
    {
        Debug.Log("Image Target Name: " + msg.imageTargetName);
        Debug.Log("Camera's Position w.r.t Image Target: " + msg.cameraPosition);
        Debug.Log("Ray Direction in WorldSpace: " + msg.rayDirection);


        // trace the ray on the hololens here
        // Raycast against all GameObjects that are on spatial mesh
        int layerMask = 1 << LayerMask.NameToLayer("SpatialMesh");

        //construct a Ray using the camera's position and tap direction
        Vector3 origin = msg.cameraPosition + this.gameObject.transform.position;
        Ray tapRay = new Ray(origin, msg.rayDirection);

        //Raycast using constructed Ray and store collisions in array hits
        RaycastHit[] hits = Physics.RaycastAll(tapRay, float.MaxValue, layerMask);

        string dm = "";
        Vector3 tmpPos = new Vector3(0, 0, 0);
        if (hits.Length > 0)
        {
            foreach (RaycastHit hit in hits)
            {
                dm += string.Format("Hit Object **\"**{0}**\"** at position **\"**{1}**\"**", hit.collider.gameObject, hit.point);
                tmpPos = hit.point - this.gameObject.transform.position;
            }
        }
        else
        {
            dm += "Nothing was hit.";
        }

        // sends information back to the Android
        RayCastHololensMessage hololensMsg = new RayCastHololensMessage()
        {
            targetPosition = tmpPos,
            debugMsg = dm
        };

        NetworkClient.Send(hololensMsg);
    }

    public void onRayCastButtonClick()
    {
        rayCastOn = !rayCastOn;
    }

    public void onImageTargetLost() {
        rayCastOn = false;
    }

    public void onImageTargetFound()
    {
        rayCastOn = true;
    }
}
