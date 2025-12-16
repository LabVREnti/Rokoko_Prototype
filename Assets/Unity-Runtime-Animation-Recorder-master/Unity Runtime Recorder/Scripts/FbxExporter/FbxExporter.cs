using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text.RegularExpressions;

public class FbxExporter : MonoBehaviour
{
    // Rutas
    public string sourceFilePath;     // FBX plantilla (en editor lo puedes poner a mano)
    public string exportFileFolder;   // Carpeta donde se guardan los FBX exportados
    public string exportFilePath;     // Archivo FBX resultante

    FbxObjectsManager fbxObj;
    FbxConnectionsManager fbxConn;

    // objs to track
    Transform[] observeTargets;
    TransformTracker[] trackers;
    int objNums = -1;

    // record operation settings
    public KeyCode startRecordKey = KeyCode.Q;
    public KeyCode endRecordKey = KeyCode.W;

    // export settings
    public bool includePathName = false;
    public bool recordPos = true;
    public bool recordRot = true;
    public bool recordScale = true;

    // for recording
    bool isRecording = false;
    bool ready = false;   // <- nuevo flag para saber si todo está inicializado

    void Awake()
    {
        // Fuerza a que siempre grabemos las tres cosas, ignorando lo que haya quedado serializado
        recordPos = true;
        recordRot = true;
        recordScale = true;
    }

    // Use this for initialization
    void Start()
    {
#if UNITY_EDITOR
        // --- EDITOR ---
        // Si no has puesto carpeta de export, usamos la del proyecto por defecto
        if (string.IsNullOrEmpty(exportFileFolder))
        {
            // Carpeta del proyecto (puedes cambiarlo si quieres)
            exportFileFolder = Path.Combine(Application.dataPath, "../Exports");
        }
        if (!Directory.Exists(exportFileFolder))
            Directory.CreateDirectory(exportFileFolder);

        // Si no has puesto nombre de archivo, generamos uno
        if (string.IsNullOrEmpty(exportFilePath))
        {
            exportFilePath = Path.Combine(
                exportFileFolder,
                "capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".fbx"
            );
        }

        // En editor asumimos que tú controlas sourceFilePath (como antes)
        if (string.IsNullOrEmpty(sourceFilePath))
        {
            Debug.LogWarning("FbxExporter: 'sourceFilePath' está vacío en el Editor. " +
                             "Asegúrate de asignar la ruta al FBX plantilla si la necesitas.");
        }

        ready = true; // en editor dejamos que funcione tal cual
#else
        // --- BUILD / RUNTIME ---
        try
        {
            // Carpeta de export: algo seguro y escribible
            exportFileFolder = Path.Combine(Application.persistentDataPath, "FbxExports");
            if (!Directory.Exists(exportFileFolder))
                Directory.CreateDirectory(exportFileFolder);

            exportFilePath = Path.Combine(
                exportFileFolder,
                "capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".fbx"
            );

            // El template debe venir dentro del build,
            // lo buscamos en StreamingAssets/FbxTemplate.fbx
            string templatePath = Path.Combine(Application.streamingAssetsPath, "FbxTemplate.fbx");
            if (!File.Exists(templatePath))
            {
                Debug.LogError("FbxExporter: No se ha encontrado 'FbxTemplate.fbx' en StreamingAssets.\n" +
                               "Crea la ruta Assets/StreamingAssets/FbxTemplate.fbx en el proyecto " +
                               "y vuelve a hacer el build.");
                ready = false;
            }
            else
            {
                sourceFilePath = templatePath;
                ready = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("FbxExporter: error al inicializar rutas en runtime: " + e);
            ready = false;
        }
#endif

        if (ready)
        {
            SetupRecordItems();
        }
        else
        {
            Debug.LogError("FbxExporter: no está listo (ready == false). No se grabarán animaciones.");
        }
    }

    void SetupRecordItems()
    {
        // get all record objs
        observeTargets = gameObject.GetComponentsInChildren<Transform>();
        trackers = new TransformTracker[observeTargets.Length];

        objNums = trackers.Length;

        for (int i = 0; i < objNums; i++)
        {

            string namePath = observeTargets[i].name;

            // if there are some nodes with same names, include path
            if (includePathName)
            {
                namePath = AnimationRecorderHelper.GetTransformPathName(transform, observeTargets[i]);
                Debug.Log("get name: " + namePath);
            }
            trackers[i] = new TransformTracker(observeTargets[i], recordPos, recordRot, recordScale);

        }
        Debug.Log("FbxExporter: setting complete");
    }


    // Update is called once per frame
    void Update()
    {
        if (!ready) return;

        if (Input.GetKeyDown(startRecordKey))
            StartRecording();

        if (Input.GetKeyDown(endRecordKey))
            EndRecording();
    }

    void StartRecording()
    {
        if (!ready)
        {
            Debug.LogError("FbxExporter: no se puede empezar a grabar, ready == false.");
            return;
        }

        isRecording = true;
        Debug.Log("FbxExporter: Start Recording");
    }

    void EndRecording()
    {
        if (!ready) return;

        isRecording = false;
        Debug.Log("FbxExporter: End Recording");

        StartCoroutine(ExportToFile());
    }

    void LateUpdate()
    {
        if (!ready) return;
        if (!isRecording) return;

        if (trackers == null)
        {
            Debug.LogError("FbxExporter: trackers es null. ¿Falló SetupRecordItems?");
            return;
        }

        for (int i = 0; i < trackers.Length; i++)
        {
            if (trackers[i] != null)
                trackers[i].recordFrame();
        }
    }

    void ModifyDefinitions(string targetFilePath)
    {
        Debug.Log("Generate Correct Definition Node ..");

        FbxDataNode[] nodes = FbxDataNode.FetchNodes(File.ReadAllText(targetFilePath), 0);
        int defIndex = 0;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].nodeName == "Definitions")
            {
                defIndex = i;
                break;
            }
        }

        FbxDataNode AnimationCurveNode = new FbxDataNode("ObjectType", "\"AnimationCurveNode\"", 1);
        AnimationCurveNode.addSubNode(new FbxDataNode("Count", (observeTargets.Length * 3).ToString(), 2));

        FbxDataNode ObjectTemplateNode = new FbxDataNode("PropertyTemplate", "\"FbxAnimCurveNode\"", 2);
        FbxDataNode propertiesNode = new FbxDataNode("Properties70", " ", 3);
        propertiesNode.addSubNode(new FbxDataNode("P", "\"d\", \"Compound\", \"\", \"\"", 4));

        ObjectTemplateNode.addSubNode(propertiesNode);
        AnimationCurveNode.addSubNode(ObjectTemplateNode);


        FbxDataNode AnimationCurve = new FbxDataNode("ObjectType", "\"AnimationCurve\"", 1);
        AnimationCurve.addSubNode(new FbxDataNode("Count", "10", 2));

        nodes[defIndex].addSubNode(AnimationCurveNode);
        nodes[defIndex].addSubNode(AnimationCurve);

        Debug.Log("Replacing Definition Node ..");

        // find line
        StreamReader reader = new StreamReader(targetFilePath);
        string headContent = "";
        string footContent = "";

        while (reader.Peek() != -1)
        {
            string line = reader.ReadLine();

            if (line.IndexOf("Definitions") != -1)
            {
                break;
            }
            else
                headContent += line + "\n";
        }

        int bracketNum = 1;

        while (reader.Peek() != -1)
        {
            string line = reader.ReadLine();

            if (line.IndexOf("{") != -1)
            {
                ++bracketNum;
            }
            else if (line.IndexOf("}") != -1)
            {
                --bracketNum;

                if (bracketNum == 0)
                    break;
            }
        }

        footContent = reader.ReadToEnd();
        reader.Close();
        string defResultData = nodes[defIndex].getResultData();

        File.WriteAllText(targetFilePath, headContent + defResultData + footContent);
    }

    IEnumerator ExportToFile()
    {
        // --- Comprobaciones iniciales del archivo plantilla ---
        if (string.IsNullOrEmpty(sourceFilePath))
        {
            Debug.LogError("FbxExporter: sourceFilePath está vacío. No puedo copiar el FBX plantilla.");
            yield break;
        }
        if (!File.Exists(sourceFilePath))
        {
            Debug.LogError("FbxExporter: no se ha encontrado el archivo plantilla en: " + sourceFilePath);
            yield break;
        }

        Debug.Log("FbxExporter: copy file ...");

        // Aseguramos la carpeta de export
        string folder = Path.GetDirectoryName(exportFilePath);
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        // Copiamos el template limpiando PreRotation
        using (StreamWriter writer = new StreamWriter(exportFilePath))
        using (StreamReader reader = new StreamReader(sourceFilePath))
        {
            while (reader.Peek() != -1)
            {
                string strLine = reader.ReadLine();

                // find prerotation
                if (strLine.IndexOf("PreRotation") != -1)
                {
                    // find tabs before 
                    int tabNum = strLine.IndexOf("P:") + 1;
                    string tabStr = "";

                    for (int i = 0; i < tabNum; i++)
                        tabStr += "\t";

                    // replace whole line
                    writer.WriteLine(tabStr + "P: \"PreRotation\", \"Vector3D\", \"Vector\", \"\",0,0,0");
                }
                else
                {
                    writer.WriteLine(strLine);
                }
            }
        }

        yield return null;

        Debug.Log("fetch nodes ...");

        // Leemos el FBX recién escrito
        string fbxText = File.ReadAllText(exportFilePath);
        if (string.IsNullOrEmpty(fbxText))
        {
            Debug.LogError("FbxExporter: el archivo FBX está vacío. exportFilePath = " + exportFilePath);
            yield break;
        }

        FbxDataNode[] allNodes = FbxDataNode.FetchNodes(fbxText, 0);

        if (allNodes == null || allNodes.Length == 0)
        {
            Debug.LogError("FbxExporter: no se ha podido parsear ningún nodo del FBX. " +
                           "Revisa la plantilla/archivo base en sourceFilePath.\n" +
                           "exportFilePath = " + exportFilePath);
            yield break;
        }

        int objNodeIndex = -1;
        for (int i = 0; i < allNodes.Length; i++)
        {
            if (allNodes[i].nodeName == "Objects")
            {
                objNodeIndex = i;
                break;
            }
        }

        if (objNodeIndex == -1)
        {
            Debug.LogError("FbxExporter: no se ha encontrado ningún nodo 'Objects' en el FBX. " +
                           "Es probable que la plantilla no sea un FBX completo.");
            yield break;
        }

        yield return null;

        // --- Setup converter ---
        fbxObj = new FbxObjectsManager(allNodes[objNodeIndex], exportFileFolder);
        fbxConn = new FbxConnectionsManager(fbxText);

        string animBaseLayerId = fbxConn.getAnimBaseLayerId();

        Debug.Log("Generating Nodes ...");

        // --- Generación de nodos de animación ---
        for (int i = 0; i < observeTargets.Length; i++)
        {
            // No grabamos el root si es este script
            if (observeTargets[i] == transform)
                continue;

            TransformTracker objTracker = trackers[i];
            if (objTracker == null)
                continue;

            // IDs necesarios
            string objName = observeTargets[i].name;
            string objId = fbxConn.searchObjectId(objName);

            string animCurveNodeT_id = getNewId();
            string animCurveNodeR_id = getNewId();
            string animCurveNodeS_id = getNewId();

            string curveT_X_id = getNewId();
            string curveT_Y_id = getNewId();
            string curveT_Z_id = getNewId();

            string curveR_X_id = getNewId();
            string curveR_Y_id = getNewId();
            string curveR_Z_id = getNewId();

            string curveS_X_id = getNewId();
            string curveS_Y_id = getNewId();
            string curveS_Z_id = getNewId();

            Debug.Log("Generating Node [" + objName + "]");

            // CurveNodes
            fbxObj.AddAnimationCurveNode(
                animCurveNodeT_id,
                FbxAnimationCurveNodeType.Translation,
                ExportHelper.UnityToMayaPosition(observeTargets[i].localPosition)
            );
            fbxObj.AddAnimationCurveNode(
                animCurveNodeR_id,
                FbxAnimationCurveNodeType.Rotation,
                ExportHelper.UnityToMayaRotation(observeTargets[i].localRotation)
            );
            fbxObj.AddAnimationCurveNode(
                animCurveNodeS_id,
                FbxAnimationCurveNodeType.Scale,
                observeTargets[i].localScale
            );

            // ========= POS =========
            int posCount = (objTracker.posDataList != null) ? objTracker.posDataList.Count : 0;
            if (posCount > 0)
            {
                float[] xDataPos = new float[posCount];
                float[] yDataPos = new float[posCount];
                float[] zDataPos = new float[posCount];

                for (int f = 0; f < posCount; f++)
                {
                    Vector3 mayaPos = ExportHelper.UnityToMayaPosition(objTracker.posDataList[f]);
                    xDataPos[f] = mayaPos.x;
                    yDataPos[f] = mayaPos.y;
                    zDataPos[f] = mayaPos.z;
                }

                fbxObj.AddAnimationCurve(curveT_X_id, xDataPos);
                fbxObj.AddAnimationCurve(curveT_Y_id, yDataPos);
                fbxObj.AddAnimationCurve(curveT_Z_id, zDataPos);
            }

            // ========= ROT =========
            int rotCount = (objTracker.rotDataList != null) ? objTracker.rotDataList.Count : 0;
            if (rotCount > 0)
            {
                float[] xDataRot = new float[rotCount];
                float[] yDataRot = new float[rotCount];
                float[] zDataRot = new float[rotCount];

                for (int f = 0; f < rotCount; f++)
                {
                    Vector3 mayaRot = ExportHelper.UnityToMayaRotation(objTracker.rotDataList[f]);
                    xDataRot[f] = mayaRot.x;
                    yDataRot[f] = mayaRot.y;
                    zDataRot[f] = mayaRot.z;
                }

                fbxObj.AddAnimationCurve(curveR_X_id, xDataRot);
                fbxObj.AddAnimationCurve(curveR_Y_id, yDataRot);
                fbxObj.AddAnimationCurve(curveR_Z_id, zDataRot);
            }

            // ========= SCALE =========
            int scaleCount = (objTracker.scaleDataList != null) ? objTracker.scaleDataList.Count : 0;
            if (scaleCount > 0)
            {
                float[] xDataScale = new float[scaleCount];
                float[] yDataScale = new float[scaleCount];
                float[] zDataScale = new float[scaleCount];

                for (int f = 0; f < scaleCount; f++)
                {
                    xDataScale[f] = objTracker.scaleDataList[f].x;
                    yDataScale[f] = objTracker.scaleDataList[f].y;
                    zDataScale[f] = objTracker.scaleDataList[f].z;
                }

                fbxObj.AddAnimationCurve(curveS_X_id, xDataScale);
                fbxObj.AddAnimationCurve(curveS_Y_id, yDataScale);
                fbxObj.AddAnimationCurve(curveS_Z_id, zDataScale);
            }

            // Conexiones
            fbxConn.AddConnectionItem("AnimCurveNode", "T", animCurveNodeT_id, "Model", objName, objId, "OP", "Lcl Translation");
            fbxConn.AddConnectionItem("AnimCurveNode", "R", animCurveNodeR_id, "Model", objName, objId, "OP", "Lcl Rotation");
            fbxConn.AddConnectionItem("AnimCurveNode", "S", animCurveNodeS_id, "Model", objName, objId, "OP", "Lcl Scaling");

            fbxConn.AddConnectionItem("AnimCurveNode", "T", animCurveNodeT_id, "AnimLayer", "BaseLayer", animBaseLayerId, "OO", "");
            fbxConn.AddConnectionItem("AnimCurveNode", "R", animCurveNodeR_id, "AnimLayer", "BaseLayer", animBaseLayerId, "OO", "");
            fbxConn.AddConnectionItem("AnimCurveNode", "S", animCurveNodeS_id, "AnimLayer", "BaseLayer", animBaseLayerId, "OO", "");

            if (posCount > 0)
            {
                fbxConn.AddConnectionItem("AnimCurve", "", curveT_X_id, "AnimCurveNode", "T", animCurveNodeT_id, "OP", "d|X");
                fbxConn.AddConnectionItem("AnimCurve", "", curveT_Y_id, "AnimCurveNode", "T", animCurveNodeT_id, "OP", "d|Y");
                fbxConn.AddConnectionItem("AnimCurve", "", curveT_Z_id, "AnimCurveNode", "T", animCurveNodeT_id, "OP", "d|Z");
            }

            if (rotCount > 0)
            {
                fbxConn.AddConnectionItem("AnimCurve", "", curveR_X_id, "AnimCurveNode", "R", animCurveNodeR_id, "OP", "d|X");
                fbxConn.AddConnectionItem("AnimCurve", "", curveR_Y_id, "AnimCurveNode", "R", animCurveNodeR_id, "OP", "d|Y");
                fbxConn.AddConnectionItem("AnimCurve", "", curveR_Z_id, "AnimCurveNode", "R", animCurveNodeR_id, "OP", "d|Z");
            }

            if (scaleCount > 0)
            {
                fbxConn.AddConnectionItem("AnimCurve", "", curveS_X_id, "AnimCurveNode", "S", animCurveNodeS_id, "OP", "d|X");
                fbxConn.AddConnectionItem("AnimCurve", "", curveS_Y_id, "AnimCurveNode", "S", animCurveNodeS_id, "OP", "d|Y");
                fbxConn.AddConnectionItem("AnimCurve", "", curveS_Z_id, "AnimCurveNode", "S", animCurveNodeS_id, "OP", "d|Z");
            }

            yield return null;
        }

        Debug.Log("Edit Definitions");
        ModifyDefinitions(exportFilePath);
        yield return null;

        Debug.Log("Edit Objects Data");
        fbxObj.EditTargetFile(exportFilePath);
        yield return null;

        Debug.Log("Edit Connections Data");
        fbxConn.EditTargetFile(exportFilePath);
        yield return null;

        // clear data
        fbxObj.objMainNode.clearSavedData();

        Debug.Log("End Exporting");
    }

    // generate ID
    int nowIdNum = 6000001;

    string getNewId()
    {
        return (nowIdNum++).ToString();
    }
}
