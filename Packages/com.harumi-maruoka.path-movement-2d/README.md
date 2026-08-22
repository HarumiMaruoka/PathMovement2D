# Path Movement 2D

Unity 2D向けの、距離基準Path・時間進行Curve・Followerを分離した経路移動パッケージです。

```text
Normalized Time → ProgressCurve → Normalized Path Distance → Path2D → Position / Tangent
```

Linear／3次Bezierの経路、独自Progress Curve、Scene View／Prefab Mode編集、Loop、PingPong、進行方向への回転、Editor Previewに対応しています。

## 動作環境

- Unity 6000.0以降
- Namespace: `Game.PathMovement2D`
- Package ID: `com.harumi-maruoka.path-movement-2d`
- FollowerはTransformを直接更新します。Rigidbody2Dは不要です。

## Package構成

```text
Packages/com.harumi-maruoka.path-movement-2d/
├── Runtime/
│   ├── Curves/
│   ├── Followers/
│   └── Path/
├── Editor/
│   ├── Curves/
│   ├── Followers/
│   └── Path/
└── Tests/Editor/
```

Assembly Definition:

- `Game.PathMovement2D.Runtime`
- `Game.PathMovement2D.Editor`
- `Game.PathMovement2D.Editor.Tests`

このリポジトリではPackageが`Packages`以下に埋め込まれ、`Packages/manifest.json`から参照されています。別プロジェクトへ移す場合はPackageフォルダをコピーし、Package ManagerからDisk上の`package.json`を指定してください。

## クイックスタート

1. **GameObject > 2D Object > Path Movement 2D > Path** を選びます。
2. 作成された`Path 2D`を選択し、Scene Viewで経路を編集します。
3. 移動させるGameObjectへ`PathFollower2D`を追加します。
4. Followerの`Path`へ作成した`Path2D`を割り当てます。
5. `Duration`または`Speed`を設定します。
6. Progress Curve、Loop、回転などを設定し、Play Modeへ入ります。

Pathを保持するGameObjectと移動対象は通常は別々にしてください。Local Pathを持つTransform自身をFollowerで移動すると、経路の基準座標も一緒に移動します。

## Path2D

`Path2D`は「どこを通るか」だけを表します。再生時間や移動状態は持ちません。

### Path設定

| 項目 | 内容 |
|---|---|
| Space | Point座標をLocalまたはWorldで保存・評価します |
| Closed | 最後のPointから最初のPointへ接続します |
| Arc Length Tolerance | 距離Lookup Table生成時の分割許容誤差です |
| Max Subdivision Depth | Bezierの適応的分割回数の上限です |

Bezierは曲がりが強い部分ほど細かく分割され、累積距離Lookup Tableへ保存されます。`EvaluatePosition(0.5f)`はBezier parameterの0.5ではなく、Path全長の50%地点を返します。

### Point設定

各PointはPosition、In/Out Tangent、Tangent Mode、Segment To Next、将来のPointイベント用永続IDを持ちます。`Segment To Next`は、そのPointから次のPointまでを`Linear`または`Bezier`のどちらで評価するかを表します。

| Tangent Mode | 動作 |
|---|---|
| Auto | 前後Pointからhandleを自動計算します |
| Mirrored | 反対側handleを逆方向・同じ長さに保ちます |
| Aligned | 方向を一直線に保ち、長さは独立させます |
| Free | In/Out handleを独立して編集します |

### Scene View操作

- Pointをクリック: 選択
- Ctrl/Cmd + Pointクリック: 選択を追加・解除
- Position Handleをドラッグ: 選択Pointを移動
- Tangent Handleをドラッグ: Bezier tangentを編集
- Shift + 左クリック: クリック位置を末尾Pointとして追加
- Delete / Backspace: 選択Pointを削除
- Inspector: Position、Tangent Mode、Segment Type、tangent数値を編集

Undo/Redoに対応しています。Local座標を使用すればPrefab Modeでも同じ操作で編集でき、Prefab rootを移動しても経路が追従します。

### 座標系

`Local`ではPointを`Path2D.transform`基準で保存し、評価結果をWorld座標へ変換します。Prefab用途では通常こちらを使用します。

`World`ではPoint値を絶対World座標として扱います。`Path2D`のTransformを動かしても経路は移動しません。

Space切替時は既存Point値を座標変換せず、値の解釈だけを変更します。経路作成後に切り替える場合はPoint座標を調整してください。

### Path Runtime API

```csharp
using Game.PathMovement2D;
using UnityEngine;

public sealed class PathSample : MonoBehaviour
{
    [SerializeField] private Path2D path;

    private void Start()
    {
        float length = path.GetLength();
        Vector2 halfway = path.EvaluatePosition(0.5f);
        Vector2 direction = path.EvaluateTangent(0.5f);
        Debug.Log($"Length: {length}, Position: {halfway}, Tangent: {direction}");
    }
}
```

Point追加・挿入・削除APIは自動的に距離テーブルをRebuildします。取得済みPointを直接変更した場合は`Rebuild()`を呼んでください。

```csharp
path.AddPoint(new Vector2(3f, 2f));
path.InsertPoint(1, new PathPoint2D(new Vector2(0f, 2f)));
path.RemovePointAt(2);

PathPoint2D first = path.GetPoint(0);
first.Position = new Vector2(-4f, 1f);
first.SegmentType = PathSegmentType.Bezier;
path.Rebuild();
```

## ProgressCurve

`ProgressCurve`は「normalized timeに対してPath上をどこまで進むか」を表します。Pathの形状や再生状態には依存しません。

```text
X = Normalized Time (0～1)
Y = Normalized Path Progress
```

### Curve編集

- グラフをダブルクリック: Key追加
- Keyをクリック／ドラッグ: 選択、Time・Value変更
- Tangent Handleをドラッグ: Curve形状変更
- Add Key / Delete Key: Key追加・削除
- 選択Keyの数値欄: Time、Value、Tangent Mode、Handleを直接編集
- Preset: Linear、Ease In、Ease Out、Ease In Out、Smooth

端点Keyは削除できません。Preset適用後にKeyまたはhandleを変更すると`Custom`になります。

### Monotonic / Free

`Monotonic`では始点`(0, 0)`、終点`(1, 1)`、Key ValueとBezier control pointの非減少を保証し、Key間の局所的な逆行を防ぎます。

`Free`ではValueの逆行と0～1範囲外へのオーバーシュートを許可します。ただし時間の関数として評価できるよう、handleのX方向は隣接Keyの時間範囲内に制限します。

Free Curveの最終出力:

- Loop: `Mathf.Repeat`でwrap
- Once: `Mathf.Clamp01`
- PingPong: `Mathf.Clamp01`

## PathFollower2D

`PathFollower2D`は時間を進め、Progress CurveとPathを評価して対象Transformへ反映します。

| 項目 | 内容 |
|---|---|
| Path | 使用する`Path2D` |
| Target | 移動対象。未指定ならFollower自身 |
| Timing Mode | DurationまたはSpeed |
| Duration | Pathを1回進む秒数 |
| Speed | Path上のunits/sec。Path長から所要時間を算出 |
| Progress Curve | normalized timeからpath progressへの変換 |
| Loop Mode | Once、Loop、PingPong |
| Initial Direction | ForwardまたはBackward |
| Update Mode | Update、LateUpdate、FixedUpdate |
| Start Offset | Path全長に対するnormalized distance offset |
| Rotate Along Path | Tangent方向へZ回転 |
| Rotation Offset Degrees | Sprite基準方向を補正するZ角度 |
| Play On Awake | Start時に自動再生 |
| Use Unscaled Time | `Time.timeScale`の影響を受けない時間を使用 |

`Start Offset`はCurve評価後のprogressへ加算されます。Loopではwrapされ、OnceとPingPongではclampされます。

### 再生API

```csharp
follower.Play();          // Initial Directionの始点から再生
follower.PlayForward();   // 現在位置から正方向へ再生
follower.PlayBackward();  // 現在位置から逆方向へ再生
follower.Pause();
follower.Resume();
follower.Stop();          // 現在Direction側の始点へ戻す
follower.SetProgress(0.5f);
```

`SetProgress`の引数はPath距離そのものではなく、Progress Curveへ入力するnormalized timelineです。Ease Curveでは`SetProgress(0.5f)`がPath距離50%になるとは限りません。評価後の距離進行度は`CurrentProgress`で取得できます。

```csharp
PathPlaybackState state = follower.State;
float normalizedTime = follower.NormalizedTime;
float pathProgress = follower.CurrentProgress;
float seconds = follower.EffectiveDuration;
```

Speed modeの`EffectiveDuration`は`Path Length / Speed`です。

### Runtimeイベント

通知はC# eventとして提供します。

```csharp
private void OnEnable()
{
    follower.Started += HandleStarted;
    follower.Completed += HandleCompleted;
    follower.Looped += HandleLooped;
}

private void OnDisable()
{
    follower.Started -= HandleStarted;
    follower.Completed -= HandleCompleted;
    follower.Looped -= HandleLooped;
}
```

利用可能なイベントは`Started`、`Paused`、`Resumed`、`Looped`、`Completed`、`Stopped`です。`Completed`はOnceが終端へ到達したとき、`Looped`はLoopまたはPingPongの境界通過時に発生します。

## Editor Preview

Follower InspectorからPreview Time、Play/Pause、Stop、Equal Time Markers、Marker Countを操作できます。

Preview位置はScene Viewへマゼンタ色のghost markerとして描画されます。対象Transformを変更しないため、Scene dirty、Prefab override、意図しないTransform保存は発生しません。

等時間マーカーはProgress Curveを一定時間間隔で評価します。マーカーが密集する場所は移動が遅く、間隔が広い場所は移動が速いことを示します。

## パフォーマンス

- 毎フレームのPath評価で管理ヒープのallocationは行いません。
- normalized distanceからArc Length sampleを二分探索します。
- Bezier位置と接線は解析式で評価します。
- Arc Length Tableはシリアライズされ、通常のRuntime再生中には再構築しません。
- Path編集、`OnValidate`、明示的な`Rebuild()`でテーブルを更新します。

`Arc Length Tolerance`を小さくするほど距離精度とsample数が増えます。通常は既定値のまま使用してください。

## テスト

EditModeテストは`Tests/Editor`にあります。コマンドライン例:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe' `
  -batchmode `
  -nographics `
  -projectPath 'C:\Path\To\Project' `
  -runTests `
  -testPlatform EditMode `
  -testResults 'C:\Path\To\Project\TestResults.xml' `
  -logFile 'C:\Path\To\Project\Logs\PathMovement2D-tests.log'
```

このUnity環境では`-quit`を同時指定するとTest Runner開始前に終了したため、Test Framework自身に終了させています。

## 現在の制限事項

- Rigidbody2Dの`MovePosition` / `MoveRotation`には対応していません。
- Segment途中へ形状を維持してPointを挿入するScene操作は未実装です。
- Shift + Clickによる追加は常にPoint列の末尾です。
- Multi Object Editingには対応していません。
- Previewはghost表示であり、対象Spriteそのものは動かしません。
- PointごとのWait、Point到達イベント、Segment別Speed/Easeは未実装です。
- Local PathとFollowerを同じ移動Transformに置く構成は想定していません。

## ライセンス

配布する場合は、プロジェクト方針に合った`LICENSE.md`をPackageへ追加してください。
