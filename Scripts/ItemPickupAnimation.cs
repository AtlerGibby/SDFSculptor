using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SDFSculptor
{
    public class ItemPickupAnimation : MonoBehaviour
    {
        Vector3 start;
        Vector3 end;

        float time;
        public float speed;

        // Start is called before the first frame update
        void Start()
        {
            start = transform.position;
            end = transform.position + Vector3.up;
        }

        // Update is called once per frame
        void Update()
        {
            time += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, (Mathf.Sin(time * speed) + 1) / 2);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y + Time.deltaTime * 250 * speed, transform.eulerAngles.z);
        }
    }
}
