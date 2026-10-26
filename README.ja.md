# ClipRack

ClipRackは、Windows標準のクリップボード履歴からテキストを表示し、編集・変換するWPFアプリです。

ClipRack本体はMIT Licenseで提供します。詳細は[LICENSE](LICENSE)を参照してください。依存パッケージのライセンスは[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)に記載しています。

## 動作環境

- Windows 10 バージョン1809以降
- .NET 10 SDK
- Windowsのクリップボード履歴が有効であること

## 起動

```powershell
dotnet run --project .\Clippy.csproj
```

アプリはシステムトレイに常駐して起動します。トレイアイコンまたは設定した呼び出しキーから画面を開けます。画面のテーマと言語は初期設定でWindowsの設定に従います。設定画面からライト／ダーク、英語／日本語を個別に指定することもできます。

## 機能

- Windows標準クリップボード履歴のテキスト項目を表示
- 書式付きテキスト、HTML、XMLから平文を抽出
- 改行コードをCRLF、LF、CRに変換
- プレーンテキストだけのクリップボード内容をCRLFへ自動補正（書式付き、画像、未対応形式は変更しない）
- 連続する空行の整理、文字列または.NET正規表現による置換
- 順番を指定できるテキストマクロの作成・保存・実行。履歴項目の右クリックからも実行可能
- Ollamaの`/api/generate`を使ったテキスト翻訳
- ライト／ダーク表示、使用言語、自動起動、呼び出しキー、ウィンドウ表示位置の設定
- 元の履歴項目を残し、マクロの結果は新しいクリップボード項目として追加

トレイ機能には[Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon)を使用します。ライセンス情報は[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)を参照してください。

設定とマクロは、以前のバージョンとの互換性のため `%LOCALAPPDATA%\Clippy` 以下にJSON形式で保存します。クリップボード履歴の本文はClipRack独自の保存領域に保存しません。

## ビルドとパッケージ作成

PowerShellから実行します。バージョンは`Clippy.csproj`から読み取ります。

```powershell
.\scripts\build-release.ps1
.\scripts\build-installer.ps1
```

アプリ実行ファイル、インストーラー、アンインストーラーに署名するには、`CODESIGN_CERT`を設定して`-Sign`を指定します。PFX/P12証明書を使う場合は、必要に応じて`CODESIGN_PASSWORD`も設定します。

```powershell
.\scripts\build-installer.ps1 -Sign
```

成果物は`artifacts\publish\win-x64`と`installer_output`に作成します。バージョンを変更するときは[docs/version-update-checklist.md](docs/version-update-checklist.md)を確認してください。

## 制限事項

- 画像処理は後続段階です。
- 翻訳は単独機能で、マクロの処理には追加できません。
- 複雑なHTML/XMLでは、意味的な空白や改行を保持できない場合があります。
- 変換結果がWindows標準履歴に登録されたかは、履歴タブで確認してください。
- 改行補正はクリップボード更新後に行うため、Windows履歴にコピー前と補正後の両方が残る場合があります。
- Windows標準履歴APIの動作とOllama接続は、対象システムでの手動確認が済んでいません。

See [README.md](README.md) for the English version.
