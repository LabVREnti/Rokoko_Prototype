using System.Collections.Generic;
using UnityEngine;

public class RecordTransform : MonoBehaviour
{
    [SerializeField] private Transform _startingtransform;
    [SerializeField] List<Transform> _transformList;
    [SerializeField] private GameObject joint;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _startingtransform = joint.transform;
    }

    // Update is called once per frame
    void Update()
    {
        _transformList.Add(joint.transform);
    }
}
