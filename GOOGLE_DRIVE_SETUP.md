# Connect Little Guy 3000 to Google Drive

Little Guy uses your own Google Desktop OAuth client and requests only `drive.file`: files it creates or files you explicitly share with the app. It does not request access to your entire Drive. Your existing ChatGPT sign-in is separate.

1. Open [Google Cloud Console](https://console.cloud.google.com/) and choose or create a project for Little Guy 3000.
2. In APIs & Services, enable **Google Drive API** for that project.
3. Open **Google Auth Platform**. Configure the app name and contact email. For a personal Google account, choose an External audience, keep the app in Testing, and add your own Google email as a test user.
4. Under Clients, create an OAuth client of type **Desktop app**. Download its JSON file and keep it in a private folder on this PC. Never commit it to the project or send it in chat.
5. In Little Guy, open **Settings → Research and ChatGPT → Choose JSON**, select that file, then **Connect Drive**. Finish Google's sign-in and consent in your browser.
6. After research is available, say **Research [topic] and save to a Google Doc** or **Research [comparison] and save to a Google Sheet**. Reports contain source links. Google Docs is the default unless the request names a sheet/table.

If a completed report is waiting for sign-in, connect Drive and select **Save report**. The report stays in memory until replaced or the app closes; it is not silently saved to disk. If a save is interrupted after upload starts, check Drive before retrying to avoid a duplicate.

Tokens are encrypted using your Windows account. Disconnect removes Little Guy's local token; to revoke the Google grant entirely, use your Google Account's third-party connections page. A project in Google's External/Testing state can issue refresh tokens that expire after seven days; reconnect when prompted. Moving the client JSON file requires choosing its new location.

Reference: [Google desktop OAuth](https://developers.google.com/identity/protocols/oauth2/native-app), [Drive file scope](https://developers.google.com/workspace/drive/api/guides/api-specific-auth), [document conversion during upload](https://developers.google.com/workspace/drive/api/guides/manage-uploads).
