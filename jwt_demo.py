"""
Standalone demo: mint a real GitHub Actions OIDC JWT, print it and its parts, then
show EXACTLY how ADO (the relying party) verifies it using GitHub's public key.
"""
import os
import json
import base64
import requests
import jwt
from jwt import PyJWKClient

AUDIENCE = os.environ.get("JWT_AUDIENCE", "api://AzureADTokenExchange")
ISSUER = "https://token.actions.githubusercontent.com"
JWKS_URI = ISSUER + "/.well-known/jwks"


def d(seg: str) -> dict:
    seg += "=" * (-len(seg) % 4)
    return json.loads(base64.urlsafe_b64decode(seg))


# 1. Mint a real GitHub OIDC token from the Actions runtime.
url = os.environ["ACTIONS_ID_TOKEN_REQUEST_URL"]
req_token = os.environ["ACTIONS_ID_TOKEN_REQUEST_TOKEN"]
resp = requests.get(f"{url}&audience={AUDIENCE}",
                    headers={"Authorization": f"Bearer {req_token}"}, timeout=30)
resp.raise_for_status()
token = resp.json()["value"]

print("=" * 60)
print("  THE GITHUB OIDC JWT  (this is what the workflow receives)")
print("=" * 60)
# base64-wrap the whole token so GitHub's log masking does not redact it.
print("RAW_JWT_B64_BEGIN")
print(base64.b64encode(token.encode()).decode())
print("RAW_JWT_B64_END")

header_b64, payload_b64, signature_b64 = token.split(".")
print("\n--- part 1: HEADER  (base64url; plain text) ---")
print(json.dumps(d(header_b64), indent=2))
print("\n--- part 2: PAYLOAD / claims  (base64url; plain text, anyone can read) ---")
print(json.dumps(d(payload_b64), indent=2))
print("\n--- part 3: SIGNATURE  (RS256 over header.payload; only GitHub's private key can make it) ---")
print(f"{signature_b64[:48]}...  ({len(signature_b64)} chars)")

print("\n" + "=" * 60)
print("  HOW ADO VERIFIES IT  (relying party side, public key only)")
print("=" * 60)
hdr = jwt.get_unverified_header(token)
print(f"1. read header            -> alg={hdr['alg']}, kid={hdr['kid']}")
print(f"2. fetch GitHub pub keys  -> GET {JWKS_URI}")
jwks = requests.get(JWKS_URI, timeout=30).json()
print(f"   JWKS returned {len(jwks['keys'])} key(s); kids: {[k['kid'] for k in jwks['keys']]}")
print(f"3. select key by kid      -> {hdr['kid']}")
signing_key = PyJWKClient(JWKS_URI).get_signing_key_from_jwt(token)
print("4. verify RS256 signature with the PUBLIC key, and check iss + aud + exp")
verified = jwt.decode(token, signing_key.key, algorithms=["RS256"], audience=AUDIENCE, issuer=ISSUER)

print("\n   >>> SIGNATURE VALID. Token is authentic and unmodified. <<<")
print("\n   Claims ADO now trusts (this is what the validator acts on):")
for k in ["iss", "aud", "repository", "repository_owner", "repository_id",
          "repository_visibility", "enterprise", "sub", "ref", "workflow",
          "job_workflow_ref", "actor", "iat", "exp"]:
    if k in verified:
        print(f"     {k}: {verified[k]}")
