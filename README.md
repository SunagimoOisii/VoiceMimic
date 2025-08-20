# VoiceMimic

短い音声素材からボイス風シーケンスを生成する Unity エディタ拡張のためのリポジトリ。現在はプロトタイプ段階であり、以下の設計方針に基づいたコードスケルトンを含む。

## 目標
- 音声区間の切り出しやピッチ変更、正規化を GUI 操作で行えるツールを提供
- 生成結果のプレビューやファイル書き出しをサポート
- Undo/Redo や区間単位での試聴により試行錯誤を効率化

## 非目標
- 大規模オーディオミドルウェアの代替
- DAW レベルの編集やエフェクト処理
- ランタイムでのリアルタイム合成
- Unity エディタ以外での利用

## 構成
- **Model**: 音声シーケンス合成と検証を担当
- **Presenter**: View からの入力を受けて Model を呼び出し、結果を View へ橋渡し
- **View**: UI Toolkit を用いたエディタ画面
- **ScriptableObject**: 設定保存用の入れ物

## コアアルゴリズム概要
1. アセット参照の検証とサンプルレート統一
2. 区間情報の収集と検証
3. 並び替えとランダム化
4. ピッチ適用、正規化、クロスフェード、連結
5. PCM 書き出し

## クラス
### VoiceMimicModel
- `Validate` `OrderSections` `Render` `ExportWav` を公開
- 検証結果は `ValidationResult` として詳細を保持

### VoiceMimicPresenter
- ボタン押下イベントをハンドリングし、Model を呼び出す

### VoiceMimicWindow
- `EditorWindow` を継承した View 実装
- メニュー `Tools/VoiceMimic` から開く

### VoiceMimicAsset
- 設定保存用 `ScriptableObject`

## 今後の予定
- 音声処理アルゴリズムの実装
- UI の詳細な構築と操作性の向上
- 各種テストの整備

