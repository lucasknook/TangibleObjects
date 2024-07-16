using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    [SerializeField] private BuildingType _buildingType;
    private CellType _cellType = CellType.Vacant;
    private int _weight = 1;

    private HashSet<GameObject> _neighbours = new();



    public void SetWeight(int weight)
    {
        _weight = weight;
    }

    public int GetWeight()
    {
        return _weight;
    }



    public void SetCellType(CellType cellType)
    {
        _cellType = cellType;
        if (!transform.TryGetComponent<MeshRenderer>(out var mesh))
        {
            return;
        }

        mesh.material.color = cellType switch
        {
            CellType.Vacant => Color.white,
            CellType.Red => Color.magenta,
            CellType.Blue => Color.cyan,
            CellType.Green => Color.green,
            _ => Color.white,
        };
    }

    public CellType GetCellType()
    {
        return _cellType;
    }



    public void SetBuildingType(BuildingType buildingType)
    {
        _buildingType = buildingType;
    }

    public BuildingType GetBuildingType()
    {
        return _buildingType;
    }



    public void AddNeighbour(GameObject neighbour)
    {
        _neighbours.Add(neighbour);
    }

    public void RemoveNeighbour(GameObject neighbour)
    {
        try
        {
            _neighbours.Remove(neighbour);
        }
        catch
        {
            Debug.Log("RemoveNeighbour: Could not find the neighbour specified.");
        }
    }



    public void ClearNeighbours()
    {
        _neighbours = new();
    }

    /* This function updates the buildings neighbour set.
     * It scans for neighbours in a sphere with radius 2 and adds any
     * object found with the "building" tag. */
    public void UpdateNeighbourSet()
    {
        /* Clear the set */
        ClearNeighbours();

        Collider[] hitColliders = Physics.OverlapSphere(GetCenter(), 2);
        foreach (var hitCollider in hitColliders)
        {
            /* Make sure you do not add yourself to your neighbour set */
            if (hitCollider.gameObject == gameObject)
            {
                continue;
            }

            /* Make sure the gameObject is a building */
            if (!hitCollider.gameObject.CompareTag("building"))
            {
                continue;
            }

            AddNeighbour(hitCollider.gameObject);
        }
    }

    public void AddSelfToNeighbours()
    {
        foreach (GameObject neighbour in _neighbours)
        {
            neighbour.GetComponent<Building>().AddNeighbour(gameObject);
        }
    }

    public void RemoveSelfFromNeighbours()
    {
        foreach (GameObject neighbour in _neighbours)
        {
            neighbour.GetComponent<Building>().RemoveNeighbour(gameObject);
        }
    }

    public bool IsSatisfied(float toleranceThreshold)
    {
        /* Vacant building are always satisfied. Schools are also always 
         * satisfied, as they should remain stationary */
        if (_cellType == CellType.Vacant || _buildingType == BuildingType.School)
        {
            return true;
        }

        int sameTypeCount = 0;
        int differentTypeCount = 0;

        foreach (GameObject neighbour in _neighbours)
        {
            Building neighbourBuilding = neighbour.GetComponent<Building>();
            if (neighbourBuilding.GetCellType() == _cellType)
            {
                sameTypeCount += neighbourBuilding.GetWeight();
            }
            else if (neighbourBuilding.GetCellType() != CellType.Vacant)
            {
                differentTypeCount += neighbourBuilding.GetWeight();
            }
        }

        int totalNeighbours = sameTypeCount + differentTypeCount;
        if (totalNeighbours == 0)
        {
            return true; // If no neighbours, consider the building satisfied
        }

        float sameTypeRatio = (float)sameTypeCount / totalNeighbours;
        if (sameTypeRatio >= toleranceThreshold)
        {
            /* Satisfied */
            return true;
        }
        else
        {
            return false;
        }
    }



    /* We need to find the position using the mesh bounds,
     * since the model does not give us a correct position in 
     * Unity */
    public Vector3 GetCenter()
    {

        /* When the object is a placed school, and thus has no mesh renderer,
         * use its position. */
        if (!gameObject.TryGetComponent<MeshFilter>(out var meshFilter))
        {
            return gameObject.transform.position;
        }

        Mesh mesh = meshFilter.mesh;

        /* Using mesh bounds to find the center */
        Bounds bounds = mesh.bounds;
        Vector3 localCenter = bounds.center;

        /* Convert local center to world space */
        Vector3 worldCenter = gameObject.transform.TransformPoint(localCenter);

        return worldCenter;
    }
}
