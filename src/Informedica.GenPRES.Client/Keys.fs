/// <summary>
/// The browser key pair of a launch, a non-extractable ECDSA P-256 key pair. The private key stays in
/// IndexedDB under the RFC 7638 thumbprint of the public key; the public key goes to the server with the
/// launch, and the server stores it in the SessionRecord.
/// </summary>
/// <remarks>
/// WebCrypto and IndexedDB are promise- and event-based browser APIs, so the interop is one JavaScript
/// literal with thin typed F# wrappers. Keys are stored per thumbprint, so a refused launch in another tab
/// cannot remove the key of the open Session in the first tab. <c>keep</c> deletes only keys older than the
/// grace period, so two launches racing in two tabs cannot delete each other's new key.
/// </remarks>
module Keys

open Fable.Core
open Fable.Core.JsInterop
open Informedica.GenPRES.Shared.Types


[<Literal>]
let private keysDef =
    """
(() => {
    const DB = "genpres";
    const STORE = "keys";
    // no launch still in flight can own a key older than this, the Launch lifetime with a
    // margin; keys younger than
    // it are left alone by keep, whoever calls it
    const GRACE_MS = 10 * 60 * 1000;

    const b64url = bytes =>
        btoa(String.fromCharCode(...new Uint8Array(bytes)))
            .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

    // an IDBRequest as a promise
    const request = r => new Promise((resolve, reject) => {
        r.onsuccess = () => resolve(r.result);
        r.onerror = () => reject(r.error);
    });

    const openDb = () => new Promise((resolve, reject) => {
        const r = indexedDB.open(DB, 1);
        r.onupgradeneeded = () => r.result.createObjectStore(STORE);
        r.onsuccess = () => resolve(r.result);
        r.onerror = () => reject(r.error);
    });

    // run f over the key store in one transaction; the completion is awaited after f so
    // that an auto-committed transaction is still observed
    const withStore = async (mode, f) => {
        const db = await openDb();
        try {
            const tx = db.transaction(STORE, mode);
            const done = new Promise((resolve, reject) => {
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
                tx.onabort = () => reject(tx.error);
            });
            const result = await f(tx.objectStore(STORE));
            await done;
            return result;
        } finally {
            db.close();
        }
    };

    // RFC 7638: SHA-256 over the required members of an EC key, in lexicographic order,
    // without whitespace; base64url without padding. The server computes the same value
    // (ServerApi.Adapters.PublicKey.thumbprint).
    const thumbprint = async jwk => {
        const canonical = JSON.stringify({ crv: jwk.crv, kty: jwk.kty, x: jwk.x, y: jwk.y });
        return b64url(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(canonical)));
    };

    return {
        // a fresh pair; the private key is stored under the thumbprint; the public JWK text
        // is returned
        generate: async () => {
            const pair = await crypto.subtle.generateKey(
                { name: "ECDSA", namedCurve: "P-256" }, false, ["sign", "verify"]);
            const jwk = await crypto.subtle.exportKey("jwk", pair.publicKey);
            const id = await thumbprint(jwk);
            const entry = { key: pair.privateKey, createdAt: Date.now() };
            await withStore("readwrite", store => request(store.put(entry, id)));
            return JSON.stringify({ kty: jwk.kty, crv: jwk.crv, x: jwk.x, y: jwk.y });
        },
        thumbprint: json => thumbprint(JSON.parse(json)),
        // delete every stored key except this one and those younger than the grace period
        keep: id => withStore("readwrite", async store => {
            const cutoff = Date.now() - GRACE_MS;
            for (const other of await request(store.getAllKeys())) {
                if (other === id) continue;
                const entry = await request(store.get(other));
                if (!entry || !entry.createdAt || entry.createdAt < cutoff) await request(store.delete(other));
            }
        }),
        // the thumbprints of the stored keys
        list: () => withStore("readonly", store => request(store.getAllKeys()))
    };
})()
"""


// emitJsExpr evaluates the literal once on load. An Emit attribute on a jsNative value is inlined at
// every use and would run the function in the literal on every call.
let private keys: obj = emitJsExpr () keysDef


[<Emit("$0.then($1, $2)")>]
let private settle (promise: JS.Promise<'T>) (resolved: 'T -> unit) (rejected: obj -> unit) : unit = jsNative


/// Converts a promise to an Async, so the callers fit Cmd.fromAsync; a rejection raises an exception
/// with the rejection text.
let private await (promise: JS.Promise<'T>) : Async<'T> =
    Async.FromContinuations(fun (cont, econt, _) ->
        settle promise cont (fun reason -> econt (System.Exception(string reason)))
    )


/// Creates the key pair of this launch. Stores the non-extractable private key in IndexedDB under the
/// thumbprint of the public key, and returns the public key as JWK text.
let generate () : Async<PublicKey> =
    async {
        let! json = keys?generate () |> await
        return PublicKey json
    }


/// The RFC 7638 thumbprint of a public JWK, as the server computes it.
let thumbprint (PublicKey json) : Async<string> = keys?thumbprint json |> await


/// Deletes every stored private key except the key with this thumbprint and the keys younger than the
/// grace period, the Launch lifetime with a margin. Called when a Session opens with this key. A new key
/// of a launch in another tab is left alone; a later launch deletes it when it is older.
let keep (thumbprint: string) : Async<unit> = keys?keep thumbprint |> await


/// The thumbprints of the stored private keys.
let list () : Async<string[]> = keys?list () |> await
