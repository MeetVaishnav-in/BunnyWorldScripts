using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimalData", menuName = "ScriptableObjects/AnimalData", order = 1)]
public class AnimalData : ScriptableObject
{
    public string animalName;
    public Sprite mainBody;
    public Sprite[] animalBodyPartsSprites;
    public List<Vector3> bodypartsTransform;
    public List<Vector2> size;
}
