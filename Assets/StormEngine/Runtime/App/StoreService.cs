using System;
using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// Real-money store. A backend (Unity IAP, registered from the StormEngine.Store assembly)
    /// handles the platform stores; without one, Editor and Development builds use a simulated
    /// store so every purchase flow can be tested. Release builds without a backend sell nothing.
    /// </summary>
    public sealed class StoreService
    {
        public interface IBackend
        {
            string Name { get; }
            bool IsReady { get; }
            void Initialize(IList<ProductDef> products, Action<bool> done);
            string LocalizedPrice(string productId);
            /// <summary>Buys a product. The callback must run before the backend confirms the order.</summary>
            void Purchase(string productId, Action<bool, string> done);
            /// <summary>A paid order that was never granted (for example the app closed mid-purchase).</summary>
            event Action<string> Recovered;
            /// <summary>A one-time product the store says this account already owns.</summary>
            event Action<string> Restored;
        }

        /// <summary>Set by the IAP assembly at startup (see StormEngine.Store).</summary>
        public static Func<IBackend> Factory;

        IBackend _backend;
        readonly StormApp _app;
        bool _buying;

        public StoreService(StormApp app)
        {
            _app = app;
        }

        public bool Simulated { get; private set; }
        public bool IsReady => _backend != null ? _backend.IsReady : Simulated;
        public string BackendName => _backend != null ? _backend.Name : Simulated ? L.T("Simulated store") : L.T("Unavailable");

        public void Start()
        {
            _backend = Factory?.Invoke();
            if (_backend != null)
            {
                _backend.Recovered += OnRecovered;
                _backend.Restored += OnRestored;
                _backend.Initialize(_app.Content.Products, ok =>
                {
                    if (!ok) Debug.LogWarning("[StormEngine] Store did not initialize; purchases are unavailable for now.");
                });
                return;
            }
            Simulated = Application.isEditor || Debug.isDebugBuild;
        }

        public string Price(ProductDef p)
        {
            string local = _backend?.LocalizedPrice(p.Id);
            return string.IsNullOrEmpty(local) ? p.FallbackPrice : local;
        }

        /// <summary>Starts a purchase; the reward is granted on success and the game is saved.</summary>
        public void Buy(int product, Action<bool> done = null)
        {
            var def = _app.Content.Products[product];
            if (_buying || (def.OneTime && _app.Game.IsProductOwned(def.Id)))
            {
                done?.Invoke(false);
                return;
            }
            void Finish(bool ok, string message)
            {
                _buying = false;
                if (ok) Grant(product);
                else if (!string.IsNullOrEmpty(message)) _app.Ui?.Toast("close", message);
                done?.Invoke(ok);
            }
            _buying = true;
            if (_backend != null)
            {
                if (!_backend.IsReady)
                {
                    Finish(false, L.T("The store is not available right now."));
                    return;
                }
                _backend.Purchase(def.Id, Finish);
                return;
            }
            if (!Simulated)
            {
                Finish(false, L.T("Purchases are not available in this build."));
                return;
            }
            _app.Ui?.Confirm(L.T("TEST PURCHASE"), L.F("Simulated store (Editor / development build). Pretend to buy {0} for {1}?", def.Name, Price(def)),
                L.T("Buy (test)"), () => Finish(true, null), "green", () => Finish(false, null));
        }

        void Grant(int product)
        {
            var def = _app.Content.Products[product];
            if (!_app.Game.GrantProduct(product)) return;
            _app.SaveNow(false);
            _app.Sfx?.Discovery();
            _app.Ui?.Toast("gem", L.F("{0} purchased! +{1} Gems.", def.Name, Num.Int(def.Gems)));
        }

        void OnRecovered(string id)
        {
            int p = _app.Content.ProductIndex(id);
            if (p >= 0) Grant(p);
        }

        void OnRestored(string id)
        {
            int p = _app.Content.ProductIndex(id);
            if (p < 0) return;
            var def = _app.Content.Products[p];
            var g = _app.Game;
            if (!def.OneTime || g.IsProductOwned(def.Id)) return;
            // Restores the permanent parts (the title); currencies were spent on the original device.
            g.OwnedProducts.Add(def.Id);
            foreach (var cosmetic in def.Cosmetics)
                if (_app.Content.CosmeticIndex(cosmetic) >= 0) g.OwnedCosmetics.Add(cosmetic);
            _app.SaveNow(false);
            _app.Ui?.Toast("gem", L.F("{0} restored.", def.Name));
        }
    }
}
