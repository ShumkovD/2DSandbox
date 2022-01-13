using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Threading;

public class GridMap : MonoBehaviour
{
    public static GridMap Instance;
    private void Awake()
    {
        if (Instance != null && this != Instance)
            Destroy(this);
        else Instance = this;
    }

    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// 変数　　 /// /// /// /// /// /// /// /// /// // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    ///    /// ///
    /// 

    [Header("Map Size")]
    public           Vector2Int mapSize;
    public           Vector2Int chunkSize;
    [SerializeField] Vector2Int chunkAmount;
    [Header("Tile Information")]
    public GameObject parentForCollision;
    public Vector2 tileDimensions;
    public GameObject tilePrefab;
    [Header("Map Generation Information")]
    public float smoothness;
    //マップの大きさ「float」
    Vector2 mapSizeF;

    //マップの情報
    string [,]  mapID;
    static bool   [,]  mapSolid;
    Vector3[,]  tilePositions;
    bool   [,]  tileIsRendered;

    //描画の情報
    [Header("Rendering Information")]
    public Camera cam;
    public int renderingOffset;
    public int additionalMemory = 1;
    [SerializeField] Vector2Int tilesToBeRendered;

    //カメラの隅
    Vector2 camLD;
    Vector2 camUR;
    
    /// 
    /// チャンクス構造体
    /// 
    public struct Chunk
    {
        public Vector2Int ld;
        public Vector2Int ru;
    };
    public Chunk[,] chunk;

    /// 
    /// タイル構造体
    /// 

    struct PooledTile
    {
        public GameObject tileObject;
        public Vector2Int index;
        public string id;
        public SpriteRenderer tileSprite;
        public BoxCollider2D tileCollider;
        public int pooledObjectIndex;
    };

    //オブジェクトプーリング「タイル」
    static PooledTile[] renderedTile;



    /// 
    /// /// 
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// 関数　　 /// /// /// /// /// /// /// /// /// // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 


    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// マルチスレッド　　 /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /

    List<Action> functionToRun;

    void StartThreadedOperation(Action someFunction)
    {
        Thread newThread = new Thread(new ThreadStart(someFunction));
        newThread.Start();
    }

    public void QueueMainThreadFunction(Action someFunction)
    {
        functionToRun.Add(someFunction);
    }
    public void UseQueuedFunctions()
    {

        while (functionToRun.Count>0)
        {
            
            Action temp = functionToRun[0];
            functionToRun.RemoveAt(0);
            temp();
        }
    }
    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// スタート　　 /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 
    //プログラムの初期化
    void Start()
    {
        functionToRun = new List<Action>();
        //マップ生成
        GridMapOnInitialize();
        //カメラの位置を貰う
        CameraGetAngles();
        //カメラの範囲を計算する
        CameraRenderingRegion();
        //オブジェクトプールの初期化
        RenderingPreparation();
        //カメラに挟んでいるマップの描画
        CameraInitializing();
        //新しいスレッドでカメラの計算をする
        StartThreadedOperation(CameraRendering);
    }
    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// グリッドマップ　　 /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 

    //タイルマップの情報を初期化します
    void GridMapOnInitialize()
    {
        mapID =         new string [mapSize.x, mapSize.y];
        mapSolid =      new bool   [mapSize.x, mapSize.y];
        tilePositions = new Vector3[mapSize.x, mapSize.y];

        chunkAmount = new Vector2Int(mapSize.x / chunkSize.x, mapSize.y/chunkSize.y);
        chunk = new Chunk[chunkAmount.x, chunkAmount.y];
        //タイル位置を計算するために、マップの大きさを計算します
        mapSizeF = mapSize * tileDimensions;

        for(int y = 0; y < mapSize.y; y++)
            for(int x = 0; x< mapSize.x; x++)
            {
        //IDを初期化します
                mapID[x, y] = "";
                mapSolid[x, y] = false;
        //タイルの位置を計算します
                tilePositions[x, y] = new Vector3(-mapSizeF.x * 0.5f + x * tileDimensions.x, -mapSizeF.y * 0.5f + tileDimensions.y * y);
            }
        //カメラ位置で、チャンクの描画準備
        for (int y = 0, yi = 0; y < mapSize.y; y += chunkSize.y, yi++)
            for (int x = 0, xi = 0; x < mapSize.x; x += chunkSize.x, xi++)
            {

                    chunk[xi, yi].ld = new Vector2Int(x, y);
                    chunk[xi, yi].ru = new Vector2Int(x + chunkSize.x, y + chunkSize.y);
            }
        //マップ生成「bool」
        mapSolid = CreatingMap();
        //マップの平滑化
        Smoothing(smoothAmount);
        //マップにはIDを与える
        mapID = MapInitID();
        
    }
    [Header("World Map Generation Properties")] //セル・オートマトンを使ています
    public int seed;                            //マップシード
    public int upperLayerThickness;             //土の厚さ
    [Range(0,100)]
    public int topFillPercent;                  //洞窟の比率「土レイヤー」
    [Range(0,100)]
    public int cavesFillPercent;                //洞窟の比率「石レイヤー」
    public int smoothAmount;                    //セル・オートマトンを何回整理するか

    int[] mapXHeight;       //各列の高さ
    int[] groundXThickness; //各列の土の厚さ

    bool[,] CreatingMap()
    {
        //マップを作るために準備
        bool[,] mapCreated = new bool[mapSize.x, mapSize.y];
        groundXThickness = new int[mapSize.x];
        mapXHeight = new int[mapSize.x];
        //マップシード
        System.Random caveSeed = new System.Random(seed.GetHashCode());
       
        //生成アルゴリズム
        for (int x = 0; x < mapSize.x; x++)
        {
            //列の高さを計算する
            int height = Mathf.RoundToInt(mapSize.y * Mathf.PerlinNoise(x / smoothness, 0));
            //その列の土の厚さを計算する
            int groundThickness = UnityEngine.Random.Range((int)(upperLayerThickness * 0.5f), upperLayerThickness + 1);
            //その情報を保存する
            groundXThickness[x] = groundThickness;
            mapXHeight[x] = height;
            //個体マスクを作成
            for (int y = 0; y < mapSize.y; y++)
            {
                if (y > height)
                {
                    mapCreated[x, y] = false;
                } 
                else if (y < height - groundThickness)
                {
                    if(caveSeed.Next(0,100)<cavesFillPercent)
                        mapCreated[x, y] = false;
                    else mapCreated[x, y] = true;
                }
                else if(y<height && y >= height - groundThickness)
                {
                    if (caveSeed.Next(0, 100) < topFillPercent)
                        mapCreated[x, y] = false;
                    else mapCreated[x, y] = true;
                }
                
            }
        }
        return mapCreated;
    }
    //セル・オートマトンの整理
    void Smoothing(int smoothAmount)
    {
        for (int i = 0; i < smoothAmount; i++)
        {
            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y < mapXHeight[x]; y++)
                {
                    int surroudingTiles = GetSurroudingTiles(x, y);
                    if (surroudingTiles > 4)
                    {
                        mapSolid[x, y] = true;
                    }
                    else if (surroudingTiles < 4)
                    {
                        mapSolid[x, y] = false;
                    }
                }
            }
            //上空にあるタイルを削除
            for (int x = 0; x < mapSize.x; x++)
            {
                for (int y = 0; y <= mapXHeight[x]; y++)
                {
                    int surroudingTiles = GetSurroudingTiles(x, y);
                    if (surroudingTiles == 0)
                        mapSolid[x, y] = false;
                }
            }
        }
    }

    //周りタイルを計算する
    int GetSurroudingTiles(int gridX, int gridY)
    {
        int surroundTiles = 0;
        for(int nx = gridX - 1; nx <= gridX+1; nx++)
            for(int ny = gridY-1;ny<=gridY+1; ny++)
            {
                if(nx>=0 && nx<mapSize.x && ny>=0 &&ny<mapSize.y)
                {
                    if (nx != gridX || ny != gridY)
                    {
                        if (mapSolid[nx, ny])
                        {
                            surroundTiles++;
                        }
                    }
                }
            }
        return surroundTiles;
    }

    int GetSurroudingTilesHorVer(int gridX, int gridY)
    {
        int surroundTiles = 0;
  
        if (mapSolid[gridX - 1, gridY])
              surroundTiles++;
        if (mapSolid[gridX + 1, gridY])
                surroundTiles++;
        if (mapSolid[gridX, gridY + 1])
            surroundTiles++;
        if (mapSolid[gridX, gridY - 1])
            surroundTiles++;
        return surroundTiles;
    }
    //マップは個体マスクを使って、作られている。
    string[,] MapInitID()
    {
        string[,] mapCreated = new string[mapSize.x, mapSize.y];
        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                if (mapSolid[x, y])
                {
                    
                    if (y < mapXHeight[x] - groundXThickness[x])
                        mapCreated[x, y] = "Stone";
                    else mapCreated[x, y] = "Grass";
                }
                else mapCreated[x, y] = "";

            }
        }
        return mapCreated;
    }

    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// 描画　　 /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 

    //カメラの隅の計算
    void CameraGetAngles()
    {
        camLD = (Vector2)cam.ScreenToWorldPoint(new Vector3(0, 0, cam.transform.position.z));
        camUR = (Vector2)cam.ScreenToWorldPoint(new Vector3(cam.scaledPixelWidth, cam.scaledPixelHeight, cam.transform.position.z));
    }

    //カメラが描画するタイルの数を計算します
    Vector2Int cameraRenderingSizeRaw;
    void CameraRenderingRegion()
    {
        //renderingOffsetは画面外で映像を準備するため
        cameraRenderingSizeRaw = new Vector2Int(Mathf.RoundToInt((camUR.x - camLD.x) / tileDimensions.x), Mathf.RoundToInt((camUR.y - camLD.y) / tileDimensions.y));
        int x = Mathf.RoundToInt(cameraRenderingSizeRaw.x + renderingOffset*2+additionalMemory);
        int y = Mathf.RoundToInt(cameraRenderingSizeRaw.y + renderingOffset*2+additionalMemory);
        tilesToBeRendered = new Vector2Int(x, y);
        tileIsRendered = new bool[x + renderingOffset, y + renderingOffset];
    }

    Vector2Int GetTilePosition(Vector2 camLD)
    {
        float x = -0.5f * mapSizeF.x;
        float y = -0.5f * mapSizeF.y;
        return new Vector2Int(-(int)((x - camLD.x) / tileDimensions.x) - 1, -(int)((y - camLD.y) / tileDimensions.y) - 1);
    }

    Vector2Int GetTilePosition(Vector3 pos)
    {
        float x = -0.5f * mapSizeF.x;
        float y = -0.5f * mapSizeF.y;
        return new Vector2Int(-(int)((x - pos.x) / tileDimensions.x) - 1, -(int)((y - pos.y) / tileDimensions.y) - 1);
    }


    /// /////////////////////////////////////////////////////////////////////////////////////////
    /// タイルプーリング//////////////////////////////////////////////////////////////////////////
    /// /////////////////////////////////////////////////////////////////////////////////////////
    //処理が落ちないように、タイルはゲームでオブジェクトプーリングを使って配置します
    void RenderingPreparation()
    {
        renderedTile = new PooledTile[(tilesToBeRendered.x+1) * (tilesToBeRendered.y+1)];
        for (int i = 0; i < renderedTile.Length; i++)
        {
            renderedTile[i].tileObject = Instantiate(tilePrefab, parentForCollision.transform) as GameObject;
            renderedTile[i].tileCollider = renderedTile[i].tileObject.GetComponent<BoxCollider2D>();
            renderedTile[i].tileCollider.enabled = false;
            renderedTile[i].tileSprite = renderedTile[i].tileObject.GetComponent<SpriteRenderer>();
            renderedTile[i].tileObject.SetActive(false);
            renderedTile[i].pooledObjectIndex = i;
        }
    }

    ////////////////
    //スプライトの変更
    ////////////////
    /////IDで
    void ChangingSprite(string id, int i)
    {
        if (id != renderedTile[i].id)
        {
            renderedTile[i].id = id;
            if (id == "")
            {
                renderedTile[i].tileSprite.sprite = null;
                return;
            }
            
            for (int o = 0; o < SpriteManager.sprites.Length; o++)
                if (id == SpriteManager.sprites[o].name)
                {
                    renderedTile[i].tileSprite.sprite = SpriteManager.sprites[o];
                    return;
                }
        }
    }

    ////////////////
    ////////////////
    ////////////////


    GameObject CreateTile(Vector3 tilePosition, Vector2Int index, string id, Vector2Int renderingInfo)
    {
        int firstUnactive = 0;
        bool isFound = false;

        if (tileIsRendered[renderingInfo.x, renderingInfo.y])
            return null;

        for (int i = 0; i < renderedTile.Length; i++)
        {
            if (!renderedTile[i].tileObject.activeInHierarchy && !isFound)
            {
                firstUnactive = i;
                isFound = true;
            }

            if (renderedTile[i].index == index)
            {
                int surTile = GetSurroudingTilesHorVer(index.x, index.y);
                if(id == "")
                    renderedTile[i].tileCollider.enabled = false;
                else if (surTile < 4)
                    renderedTile[i].tileCollider.enabled = true;
                else renderedTile[i].tileCollider.enabled = false;

                ChangingSprite(id, i);
                if (!renderedTile[i].tileObject.activeInHierarchy)
                {
                    tileIsRendered[renderingInfo.x, renderingInfo.y] = true;
                    renderedTile[i].tileObject.SetActive(true);
                    return renderedTile[i].tileObject;
                }
                else
                {
                    tileIsRendered[renderingInfo.x, renderingInfo.y] = true;
                    return renderedTile[i].tileObject;
                }
            }
        }
        if (isFound)
        {
            ChangingSprite(id, firstUnactive);
            int surTile = GetSurroudingTilesHorVer(index.x, index.y);
            if (id == "")
                renderedTile[firstUnactive].tileCollider.enabled = false;
            else if (surTile < 4)
                renderedTile[firstUnactive].tileCollider.enabled = true;
            else renderedTile[firstUnactive].tileCollider.enabled = false;
            tileIsRendered[renderingInfo.x, renderingInfo.y] = true;
            renderedTile[firstUnactive].tileObject.SetActive(true);
            renderedTile[firstUnactive].tileObject.transform.position = tilePosition;
            renderedTile[firstUnactive].index = index;
            return renderedTile[firstUnactive].tileObject;
        }
        return null;
    }

    //描画範囲以外のタイルをオブジェクトプールに戻す
    void ClearPooledObjectsBasedOnPosition(Vector2 lb, Vector2 ru)
    {
        for (int i = 0; i < renderedTile.Length; i++)
        {
            if (!renderedTile[i].tileObject.activeInHierarchy)
            {
                continue;
            }
            if (!(renderedTile[i].index.x >= lb.x && renderedTile[i].index.x <= ru.x &&
                  renderedTile[i].index.y >= lb.y && renderedTile[i].index.y <= ru.y))
            {
                renderedTile[i].tileObject.SetActive(false);
                renderedTile[i].tileCollider.enabled = false;
            }
        }

    }

    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// チャンク　　 /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 

    public Chunk GetChunkLD(Vector2Int index)
    {
        for(int y = 0; y < chunkAmount.y; y++)
            for(int x = 0; x < chunkAmount.x; x++)
            {
                if (MyUtility.PointIsInsideOfRect(chunk[x,y].ld, chunk[x,y].ru, index))
                {
                    return chunk[x,y];
                }
            }
        return default;
    }
    public Chunk GetChunkRU(Vector2Int index)
    {
        for (int y = 0; y < chunkAmount.y; y++)
            for (int x = 0; x < chunkAmount.x; x++)
            {
                if (MyUtility.PointIsInsideOfRect(chunk[x, y].ld, chunk[x, y].ru, index))
                {
                    return chunk[x, y];
                }
            }
        return default;
    }

    ///
    /// ///
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// アップデート　　 /// /// /// /// /// /// // // /
    /// /// /// /// /// /// /// /// /// /// /// /// // /
    /// ///
    /// 

    private void Update()
    {
        //カメラの情報を貰う
        CameraGetAngles();
        //行列に入っている関数を処理する
        UseQueuedFunctions();
        if (Input.GetMouseButtonDown(0))
            ChangeTileAtCursor();
    }

    /// /////////////////////////////////////////////////////////////////////////////////////////
    /// カメラの処理//////////////////////////////////////////////////////////////////////////////
    /// /////////////////////////////////////////////////////////////////////////////////////////

    Vector2Int prevLBIndex;
    Vector2Int prevRUIndex;

    Chunk chunkLD;
    Chunk chunkRU;

    public Vector2Int LBIndex;
    public Vector2Int RUIndex;
    void CameraInitializing()
    {
        
        LBIndex = Vector2Int.zero;
        RUIndex = Vector2Int.zero;
        //マップにあるタイルのインデクスを見つける
        LBIndex = GetTilePosition(camLD);
        RUIndex = new Vector2Int(LBIndex.x + cameraRenderingSizeRaw.x, LBIndex.y + cameraRenderingSizeRaw.y);
        //使っているチャンクを見つける
        chunkLD = GetChunkLD(LBIndex);
        chunkRU = GetChunkRU(RUIndex);

        //画面にタイルを配置すること
        for (int y = LBIndex.y - renderingOffset, yRendering = 0; y <= RUIndex.y + renderingOffset; y++, yRendering++)
            for (int x = LBIndex.x - renderingOffset, xRendering = 0; x <= RUIndex.x + renderingOffset; x++, xRendering++)
            {
                CreateTile(tilePositions[x, y], new Vector2Int(x, y), mapID[x,y], new Vector2Int(xRendering, yRendering));
            }
        prevRUIndex = RUIndex;
        prevLBIndex = LBIndex;
    }

    //位置の変更を確定するために
    public Vector2 renderingLB;
    public Vector2 renderingRU;

    //カメラの計算を別のスレッドで行う
    public void CameraRendering()
    {

        while (true)
        {
            //左下のインデクスを探します
            LBIndex = GetTilePosition(camLD);
            RUIndex = new Vector2Int(LBIndex.x + cameraRenderingSizeRaw.x, LBIndex.y + cameraRenderingSizeRaw.y);
            //チャンク計算
            chunkLD = GetChunkLD(LBIndex - new Vector2Int(renderingOffset, renderingOffset));
            chunkRU = GetChunkRU(RUIndex + new Vector2Int(renderingOffset, renderingOffset));
          

            //前のフレームのインデクスとこのフレームのインデクスは同じだったら、何もしません
            if (prevLBIndex != LBIndex || prevRUIndex != RUIndex)
            {
                //描画範囲を計算する
                renderingLB = new Vector2(LBIndex.x - renderingOffset, LBIndex.y - renderingOffset);
                renderingRU = new Vector2(RUIndex.x + renderingOffset, RUIndex.y + renderingOffset);

                prevRUIndex = RUIndex;
                prevLBIndex = LBIndex;
                //マインスレッドで描画する
              QueueMainThreadFunction( MapRender);
            }
        }
    }
    //マップ描画
    private void MapRender()
    {
        ////カメラの周りに全てのタイルを設定し直す             
        ///
            ClearPooledObjectsBasedOnPosition(renderingLB, renderingRU);

        //画面にタイルを配置すること
        for (int y = LBIndex.y - renderingOffset, yRender = 0; y < RUIndex.y + renderingOffset + 1; y++, yRender++)
            for (int x = LBIndex.x - renderingOffset, xRender = 0; x < RUIndex.x + renderingOffset + 1; x++, xRender++)
            {
                tileIsRendered[xRender, yRender] = false;
                    CreateTile(tilePositions[x, y], new Vector2Int(x, y), mapID[x, y], new Vector2Int(xRender, yRender));

            }
        CheckAllRender();
    }
    //描画出来なかった際、描画します
    void CheckAllRender()
    {
        for (int y = LBIndex.y - renderingOffset, yRender = 0; y < RUIndex.y + renderingOffset + 1; y++, yRender++)
            for (int x = LBIndex.x - renderingOffset, xRender = 0; x < RUIndex.x + renderingOffset + 1; x++, xRender++)
            {
                if (!tileIsRendered[xRender, yRender])
                    CreateTile(tilePositions[x, y], new Vector2Int(x, y), mapID[x, y], new Vector2Int(xRender, yRender));
            }
    }

    /// /////////////////
    ///  タイルの入れ替え
    /// /////////////////

    //仮
    void ChangeTileAtCursor()
    {
        Vector2Int index;
        index = GetTilePosition(cam.ScreenToWorldPoint(Input.mousePosition) + (Vector3)tileDimensions* 1.5f);

        //仮　ー＞インベントリシステムを作るまで
        ChangingTile(index, "");
    }
    //
    void ChangingTile(Vector2Int index, string id)
    {
        int surroundingTile = 0;
        //タイルを崩す際
        if (id == "")
        {
           
            if (id == mapID[index.x, index.y])
                return;
            mapID[index.x, index.y] = id;
            mapSolid[index.x, index.y] = false;
        }
        else
        {
            //個体タイルを置く際
            if (id == mapID[index.x, index.y])
                return;
            //上空に置かないために、周りのタイルを確認する
            surroundingTile = GetSurroudingTilesHorVer(index.x,index.y);

            if (surroundingTile == 0)
                return;

            mapID[index.x, index.y] = id;
            mapSolid[index.x, index.y] = true;
        }
        //スプライトを変える
        for (int i = 0; i < renderedTile.Length; i++)
        {
            if(renderedTile[i].tileObject.activeInHierarchy)
            {
                if (index == renderedTile[i].index)
                {
                    ChangingSprite(id, i);
                    if (id != "")
                        renderedTile[i].tileCollider.enabled = false;
                    else if (surroundingTile < 4)
                        renderedTile[i].tileCollider.enabled = true;
                    else renderedTile[i].tileCollider.enabled = false;

                }
            }

        }
        UpdateSurroundingTiles(index);
    }

    void UpdateSurroundingTiles(Vector2Int index)
    {
        for (int nx = index.x - 1; nx <= index.x + 1; nx++)
            for (int ny = index.y - 1; ny <= index.y + 1; ny++)
            {
                if (nx >= 0 && nx < mapSize.x && ny >= 0 && ny < mapSize.y)
                {
                    for(int i = 0; i < renderedTile.Length; i++)
                    {
                        if (renderedTile[i].tileObject.activeInHierarchy && renderedTile[i].index == new Vector2Int(nx, ny))
                        { 
                            int surTiles = GetSurroudingTilesHorVer(nx, ny);
                            Debug.Log(surTiles);
                            if (surTiles<4 && !(renderedTile[i].id == ""))
                                renderedTile[i].tileCollider.enabled = true;
                            else renderedTile[i].tileCollider.enabled = false;
                        }
                    }

                  
                }
            }
    }

    private void OnDrawGizmos()
    {
        if (tilePositions == null)
            return;
        

        for (int y = 0; y < mapSize.y; y++)
        {
            if(y%chunkSize.y == 0)
                Gizmos.color = Color.red;
            else Gizmos.color = Color.white;
            Gizmos.DrawLine(tilePositions[0, y] - (Vector3)tileDimensions *0.5f, tilePositions[mapSize.x - 1, y] - (Vector3)tileDimensions * 0.5f);

        }
        for (int x = 0; x < mapSize.x; x++)
        {
            if (x % chunkSize.x == 0)
                Gizmos.color = Color.red;
            else Gizmos.color = Color.white;
            Gizmos.DrawLine(tilePositions[x, 0] - (Vector3)tileDimensions * 0.5f, tilePositions[x, mapSize.y - 1] - (Vector3)tileDimensions * 0.5f);
        }
        for (int i = 0; i < renderedTile.Length; i++)
            if (renderedTile[i].tileCollider.isActiveAndEnabled)
                Gizmos.DrawCube(renderedTile[i].tileObject.transform.position, new Vector3(0.8f, 0.8f));
        Gizmos.DrawSphere(renderingRU, 0.1f);
        Gizmos.DrawSphere(renderingLB, 0.1f);
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(camUR, 0.1f);
        Gizmos.DrawSphere(camLD, 0.1f);
    }
}
