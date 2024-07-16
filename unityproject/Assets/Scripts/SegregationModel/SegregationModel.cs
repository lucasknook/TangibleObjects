using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

public enum CellType
{
    Vacant,
    Red,
    Green,
    Blue
}

public enum BuildingType
{
    Residency,
    School
}

public class SegregationModel : MonoBehaviour 
{
    [SerializeField, Range(0, 1)] private float _toleranceThreshold;

    public GameObject grass;
    public GameObject schoolPrefab;
    public GameObject circlePrefab;

    /* Assign any object that has the YolactServer script attached. */
    public GameObject yoloServerObject;
    private YoloServer _yoloServer;

    private bool isRunning;
    private HashSet<GameObject> _instantiatedBuildings;
    private HashSet<GameObject> _staticBuildings;

    /* Width and height of the camera, to scale correctly */
    private int _max_width;
    private int _max_height;


    // Start is called before the first frame update
    void Start()
    {
        _instantiatedBuildings = new();
        _staticBuildings = new();

        foreach (GameObject buildingObject in GameObject.FindGameObjectsWithTag("building"))
        {
            _staticBuildings.Add(buildingObject);
        }

        /* Initialize the neighbour lists of all buildings */
        InitBuildings();
        RandomCells();
        isRunning = false;

        _yoloServer = yoloServerObject.GetComponent<YoloServer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            RandomCells();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isRunning)
            {
                isRunning = false;
            }
            else
            {
                isRunning = true;
            }
        }

        UpdateSchools();

        if (isRunning)
        {
            StepModel();
        }
    }

    /* This also clears the buildings' neighbour lists, so you can restart the simulation with this. */
    private void InitBuildings()
    {
        foreach (GameObject buildingObject in _staticBuildings)
        {
            
            Building building = buildingObject.GetComponent<Building>();
            BuildingType buildingType = building.GetBuildingType();
            switch (buildingType)
            {
                case BuildingType.School:
                    building.SetWeight(1000);
                    break;
                case BuildingType.Residency:
                    building.SetWeight(1);
                    break;
                default:
                    building.SetWeight(1);
                    break;
            }

            /* Update neighbours for every building */
            building.UpdateNeighbourSet();

            /* Set building types to residency */
            building.SetBuildingType(BuildingType.Residency);

            /* Make the buildings vacant */
            building.SetCellType(CellType.Vacant);
        }

        Debug.Log("Done initializing!");
    }

    private void RandomCells()
    {
        foreach (GameObject buildingObject in _staticBuildings)
        {
            Building building = buildingObject.GetComponent<Building>();
            
            CellType cellType = Random.Range(0, 95) switch
            {
                <50 => CellType.Vacant,
                <65 => CellType.Red,
                <80 => CellType.Green,
                <95 => CellType.Blue,
                _ => CellType.Vacant,
            };

            building.SetCellType(cellType);
        }
    }


    ///* The PlaceSchool function takes a 2D vector where (0, 0) is top left
    // * and (1, 1) is bottom right */
    //public void PlaceSchool(Vector2 pos, CellType cellType)
    //{
    //    float localPosX = -5 + 10 * pos.x;
    //    float localPosZ = 5 - 10 * pos.y;

    //    /* Instantiate the building */
    //    GameObject buildingObject = Instantiate(schoolPrefab, grass.transform, false);

    //    buildingObject.transform.localPosition = new Vector3(localPosX, 0f, localPosZ);
    //    buildingObject.tag = "building";

    //    Building building = buildingObject.GetComponent<Building>();
    //    building.SetBuildingType(BuildingType.School);
    //    building.SetCellType(cellType);
    //    building.SetWeight(1000);

    //    /* Add to list */
    //    _instantiatedBuildings.Add(buildingObject);

    //    /* Update neighbour set */
    //    building.UpdateNeighbourSet();

    //    /* Add self to neighbours */
    //    building.AddSelfToNeighbours();

    //    /* Show colored circle */
    //    GameObject circleObject = Instantiate(circlePrefab, grass.transform, false);
    //    circleObject.transform.localPosition = new Vector3(localPosX, 0.1f, localPosZ);
    //    circleObject.transform.localScale = circleObject.transform.parent.InverseTransformVector(new Vector3(2, 2, 2));

    //    circleObject.GetComponent<SpriteRenderer>().color = building.GetCellType() switch
    //    {
    //        CellType.Vacant => Color.white,
    //        CellType.Red => Color.magenta,
    //        CellType.Blue => Color.cyan,
    //        CellType.Green => Color.green,
    //        _ => Color.white,
    //    };

    //    /* Same for the order */
    //    circleObject.GetComponent<SpriteRenderer>().sortingOrder = building.GetCellType() switch
    //    {
    //        CellType.Vacant => 0,
    //        CellType.Red => 1,
    //        CellType.Blue => 2,
    //        CellType.Green => 3,
    //        _ => 0,
    //    };

    //    _instantiatedCircles.Add(circleObject);
    //}
    /* The PlaceSchool function takes a 2D vector where (0, 0) is top left
     * and (1, 1) is bottom right */
    public void PlaceSchool(Vector2 pos, CellType cellType)
    {
        float localPosX = -5 + 10 * pos.x;
        float localPosZ = 5 - 10 * pos.y;

        /* Show colored circle */
        GameObject circleObject = Instantiate(circlePrefab, grass.transform, false);
        circleObject.transform.localPosition = new Vector3(localPosX, 0.1f, localPosZ);
        circleObject.transform.localScale = circleObject.transform.parent.InverseTransformVector(new Vector3(2, 2, 2));
        circleObject.tag = "building";

        Building building = circleObject.GetComponent<Building>();
        building.SetBuildingType(BuildingType.School);
        building.SetCellType(cellType);
        building.SetWeight(1000);

        /* Add to the list */
        _instantiatedBuildings.Add(circleObject);

        /* Update neighbour set */
        building.UpdateNeighbourSet();

        /* Add self to neighbours */
        building.AddSelfToNeighbours();

        /* Change the color */
        circleObject.GetComponent<SpriteRenderer>().color = building.GetCellType() switch
        {
            CellType.Vacant => Color.white,
            CellType.Red => Color.magenta,
            CellType.Blue => Color.cyan,
            CellType.Green => Color.green,
            _ => Color.white,
        };

        /* Same for the order */
        circleObject.GetComponent<SpriteRenderer>().sortingOrder = building.GetCellType() switch
        {
            CellType.Vacant => 0,
            CellType.Red => 1,
            CellType.Blue => 2,
            CellType.Green => 3,
            _ => 0,
        };
    }



    private void UpdateSchools()
    {
        /* Remove all instantiated buildings */
        foreach (GameObject buildingObject in _instantiatedBuildings)
        {
            buildingObject.tag = "delete";

            Building building = buildingObject.GetComponent<Building>();

            /* This removes the deleted building from all neighbours */
            building.RemoveSelfFromNeighbours();

            Destroy(buildingObject);
        }

        /* Remove all circles */
        //foreach(GameObject circleObject in _instantiatedCircles)
        //{
        //    Destroy(circleObject);
        //}

        /* Clear the set of instantiated buildings */
        _instantiatedBuildings = new();

        /* Get info from the yolact server */
        YoloServer.BboxesWrapper bw = _yoloServer.GetBboxesWrapper();
        if (!YoloServer.CheckBboxesWrapper(bw))
        {
            return;
        }

        _max_width = bw.max_width;
        _max_height = bw.max_height;

        /* Add all schools to be displayed */
        for (int i = 0; i < bw.names.Length; i++)
        {
            float x = (bw.bboxes[i * 4] + bw.bboxes[i * 4 + 2]) / 2.0f / (float) _max_width;
            float y = (bw.bboxes[i * 4 + 1] + bw.bboxes[i * 4 + 3]) / 2.0f / (float) _max_height;

            if (bw.names[i] == "red")
            {
                PlaceSchool(new Vector2(x, y), CellType.Red);
            }
            else if (bw.names[i] == "green")
            {
                PlaceSchool(new Vector2(x, y), CellType.Green);
            }
            else if (bw.names[i] == "blue")
            {
                PlaceSchool(new Vector2(x, y), CellType.Blue);
            }
            else if (bw.names[i] == "vacant")
            {
                PlaceSchool(new Vector2(x, y), CellType.Vacant);
            }
        }
    }

    private void StepModel()
    {
        List<Building> dissatisfiedBuildings = new();
        List<Building> vacantBuildings = new();

        List<GameObject> staticBuildings = new();

        /* Loop through each non-school building */
        foreach (GameObject buildingObject in _staticBuildings)
        {
            Building building = buildingObject.GetComponent<Building>();
            if (building.GetCellType() != CellType.Vacant && building.GetBuildingType() != BuildingType.School)
            {
                if (!building.IsSatisfied(_toleranceThreshold))
                {
                    dissatisfiedBuildings.Add(building);
                }
            }

            if (building.GetCellType() == CellType.Vacant)
            {
                vacantBuildings.Add(building);
            }

            staticBuildings.Add(buildingObject);
        }

        foreach (Building dissatisfiedBuilding in dissatisfiedBuildings)
        {
            if (vacantBuildings.Count == 0)
            {
                break;
            }

            /* Move to random spot */
            GameObject randomBuildingObject = staticBuildings[Random.Range(0, staticBuildings.Count)];
            Building vacantBuilding = randomBuildingObject.GetComponent<Building>();

            //Building vacantBuilding = vacantBuildings[Random.Range(0, vacantBuildings.Count)];
            //vacantBuildings.Remove(vacantBuilding);

            /* Swap cell types */
            CellType tempCellType = dissatisfiedBuilding.GetCellType();
            dissatisfiedBuilding.SetCellType(vacantBuilding.GetCellType());
            vacantBuilding.SetCellType(tempCellType);

            /* Swap weights */
            int tempWeight = dissatisfiedBuilding.GetWeight();
            dissatisfiedBuilding.SetWeight(vacantBuilding.GetWeight());
            vacantBuilding.SetWeight(tempWeight);
        }

        Debug.Log("Model step executed!");
    }
}
