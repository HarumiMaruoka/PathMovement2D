# Movement Clip

`PathMovementClip2D` は経路、再生時間、Progress Curve、Once / Loop / PingPong、進行方向への回転を保存する ScriptableObject です。
既存の `Path2D` / `PathFollower2D` と併用できます。

## 作成と編集

1. Project の **Create > Path Movement 2D > Movement Clip** で作成します。
2. キャラクターに `PathMovementPlayer2D` を追加し、Clip を割り当てます。
3. **Edit / Preview Movement** または **Window > Path Movement 2D > Path Movement Editor** を開きます。アセットのダブルクリックでも開けます。
4. SceneView で、既存 Path2D と同じ点選択、複数選択、移動、接線編集、Shift + Click による末尾追加、Delete を使用できます。Undo / Redo に対応します。
5. ウィンドウ下部で経路点、Progress Curve、Duration、Loop Mode、回転を設定します。

SceneView の編集先は共有アセットです。別の動きを作る場合は **Duplicate Clip** でアセットを複製します。
`Mirror X` は再生・編集表示の左右を反転し、元の点データは変更しません。Sprite 自体の flipX は変更しません。

既存 Path2D のコンポーネントメニュー **Create Movement Clip** から経路をコピーできます。
開始点を原点とする相対経路へ変換し、元の Transform のスケールを経路へ反映します。形状を保持するため、変換後の接線モードは Free になります。
コピー対象は経路形状です。Follower の時間・回転設定はコピーしません。作成元との参照共有もありません。

## 再生インスタンス

保存アセットは複数キャラクターで共有しますが、再生インスタンスは対象ごとに作成します。
`PathMovementPlayer2D` は生成・破棄を管理します。独自コンポーネントでは次のように使用します。

```csharp
using UnityEngine;
using Game.PathMovement2D;

public sealed class CharacterMovement : MonoBehaviour, IPathMovementClipSource
{
    [SerializeField] private PathMovementClip2D clip;
    private PathMovementClip2D playback;

    private void Start()
    {
        playback = clip.CreatePlaybackInstance();
        playback.CaptureOrigin(transform);
    }

    private void LateUpdate() => playback.Update(transform, Time.deltaTime);

    private void OnDestroy()
    {
        if (playback != null) Destroy(playback);
    }

    public void GetPathMovementClips(System.Collections.Generic.List<PathMovementClip2D> clips)
    {
        if (clip != null) clips.Add(clip);
    }
}
```

- `Update(target, deltaTime)`：非負の秒数だけタイマーを進めて適用します。
- `Update(deltaTime)`：タイマーを進め、更新前後の位置差分をクリップ座標の `Vector2` として返します。Transform は変更しません。
- `Evaluate(target, time)`：指定秒数を適用します。タイマーは変更しません。
- `Reset()`：タイマーのみゼロに戻します。対象姿勢と基準座標は変更しません。
- `CaptureOrigin(target)`：開始位置とワールド Z 回転を再取得します。省略時は最初の適用時に取得します。
- `Sample(time, mirrorX)`：クリップ座標の位置・進行方向を返します。保存アセットでも使用できます。
- `GetNormalizedTime(time)`：再生時間とループ設定を反映した 0〜1 の時間を返します。

`Update` は Unity のライフサイクルメソッドとの衝突を避けるため、`Game.PathMovement2D` 名前空間の拡張メソッドとして提供します。

自分で移動を適用する場合は、差分版を使用します。

```csharp
Vector2 moveDelta = playback.Update(Time.deltaTime);
transform.position += new Vector3(moveDelta.x, moveDelta.y, 0f);
if (playback.IsComplete)
{
    // Once の終了処理
}
```

差分には Progress Curve と MirrorX を反映します。Space、CaptureOrigin の回転、ReferenceTransform の動きは反映しないため、必要な座標変換は呼び出し側で行ってください。
初回は時刻0の位置との差分を返すため、経路の開始点への移動は含みません。Once の完了後はゼロ、PingPong の復路は逆向きの差分を返します。
Loop は始点へ戻る差分を含みます。1周ちょうど進めると差分はゼロになります。戻り値は更新前後の変位であり、その間に通過した総移動距離ではありません。
両方の Update は同じタイマーを進めるため、通常は1フレームにどちらか一方だけ呼びます。Evaluate はタイマーを変更せず、差分版の比較元にも影響しません。

タイマーは非シリアライズです。保存アセットへの Update / Evaluate は誤共有を防ぐため例外になります。
1 インスタンスを別の対象で使い直す場合は CaptureOrigin を呼んでください。アセット編集は生成済みインスタンスに自動反映しません。

## 座標と標準 Player

標準の `Relative` は CaptureOrigin 時の位置・Z 回転を固定基準にします。対象が移動しても基準は移動しません。
対象のスケールは経路に適用せず、XY を移動して Z を維持します。点の `(0, 0)` が開始位置に対応します。
`World` は経路の XY を絶対座標として使用します。
`ReferenceTransform` は指定 Transform の現在の位置・Z 回転を基準にします。基準のスケールは使用しません。移動対象自身やその子を基準にはできません。

Player の Play は現在の姿勢を新しい基準として先頭から開始します。Pause / Resume は基準と時刻を維持します。
Stop はタイマーをゼロに戻して、保持している基準の先頭位置を適用します。クリップを差し替える際は Clip プロパティを使用してください。
更新タイミングは Update / LateUpdate / FixedUpdate、標準は LateUpdate です。
アニメーションとの同時使用では移動ルートと見た目の子階層を分けると管理しやすくなります。

## アニメーション同期プレビュー

選択 GameObject の `IPathMovementClipSource` 実装から、クリップ一覧を取得します。
複数クリップは Source Clips で選択でき、Lock Selection で対象を固定できます。
独自コンポーネントの場合、必要に応じて Movement Target / Animator を明示的に指定してください。

Animator に Controller を割り当て、Controller Animation から AnimationClip を選択します。
Override Controller は runtimeAnimatorController が返す差し替え後のクリップを使用します。

- Normalized Time Sync が ON：移動とアニメーションの開始・終了を一致させます。
- OFF：双方の秒数を一致させます。アニメーションの終了後は、そのクリップのループ設定に従いループまたは終端保持します。
- Animation Offset：アニメーション側の時刻に秒数を加えます。
- PingPong：復路ではアニメーション時刻も逆方向へ戻ります。

Play / Pause / 時間スライダーで対象とアニメーションをプレビューし、Stop / Restore で元の状態に戻します。
選択変更、ウィンドウ終了、Undo / Redo、スクリプト再読み込み、Play Mode 移行でもプレビューを終了します。
経路編集中は位置マーカーも表示します。対象未指定なら原点基準でアセットを編集できます。

移動対象の XY と、Rotate Along Path 有効時の回転は経路側を優先します。
他の Animation ウィンドウ等が AnimationMode を使用中の場合は、それを停止してから開始してください。
シーン上の対象または Prefab Mode 内の対象を使用し、Project 内の Prefab アセット自体にはプレビューを適用しません。

従来の Follower Inspector は位置マーカーのプレビュー、新しい Path Movement Editor は対象とアニメーションを動かすプレビューを提供します。
同期プレビューは単一 AnimationClip のサンプリングです。Controller の遷移・BlendTree・レイヤーブレンド、標準 Animation ウィンドウとの操作同期、実行時の Animator 自動同期は含みません。
地点イベント、待機時間、移動ブレンド、Timeline 連携、Rigidbody2D 経由の移動も今回の実装範囲外です。
