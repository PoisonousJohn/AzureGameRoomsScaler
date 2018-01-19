#!/bin/sh
curl -vvvv -H "Content-Type: application/json" -d "{ \"Region\" : \"westeurope\", \"Size\" : \"Standard_B1S\", \"ResourceGroup\": \"scaler\" }" -X POST "https://azgrsc.azurewebsites.net/api/node/create?code=PbJl2BGMMA9ykrudPybvufPbwbF5kL6WPxaWJpvzK19r2P28XmC5Wg=="
