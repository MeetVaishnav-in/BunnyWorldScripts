using UnityEngine;

[CreateAssetMenu(fileName = "JigsawPiece", menuName = "Jigsaw/Piece")]
public class JigsawPiece : ScriptableObject
{
    public int pieceIndex;
    public Sprite sprite;
}
