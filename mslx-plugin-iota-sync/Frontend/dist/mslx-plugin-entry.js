(function(){"use strict";try{if(typeof document<"u"){var e=document.createElement("style");e.appendChild(document.createTextNode(".page[data-v-59e3dd38]{padding:20px}.card[data-v-59e3dd38]{margin-top:16px}.inline[data-v-59e3dd38]{display:inline}.outline[data-v-59e3dd38]{outline-style:solid}")),document.head.appendChild(e)}}catch(d){console.error("vite-plugin-css-injected-by-js",d)}})();
var y, b;
function I() {
  return b || (b = 1, y = Vue), y;
}
var e = I(), x, S;
function F() {
  return S || (S = 1, x = TDesign), x;
}
var s = F();
const K = /* @__PURE__ */ e.defineComponent({
  __name: "SyncAdmin",
  setup(_) {
    const r = "/api/plugins/mslx-plugin-iota-sync/admin", c = e.ref(!1), i = e.ref([]), u = e.ref(""), v = e.ref(""), m = e.ref(null), T = [{ label: "客户端必需", value: 0 }, { label: "可选（默认安装）", value: 1 }, { label: "仅服务端", value: 2 }, { label: "待人工确认", value: 3 }], B = ["vanilla", "forge", "neoforge", "fabric", "quilt"].map((t) => ({ label: t, value: t })), U = [{ colKey: "path", title: "文件", ellipsis: !0 }, { colKey: "size", title: "大小" }, { colKey: "side", title: "端侧", cell: "side" }, { colKey: "reason", title: "识别依据", ellipsis: !0 }];
    async function d(t, a) {
      const l = await fetch(t, a);
      if (!l.ok) throw new Error(await l.text());
      return l.status === 204 ? null : l.json();
    }
    async function N() {
      c.value = !0;
      try {
        i.value = await d(`${r}/instances`);
        for (const t of i.value) t.settings ||= { instanceId: t.id, directories: ["mods", "config", "defaultconfigs", "kubejs", "resourcepacks", "shaderpacks"].map((a, l) => ({ path: a, enabled: l === 0 })), overrides: {}, minecraftVersion: "", loader: "", loaderVersion: "", serverAddress: "", lastScan: [], releases: [] };
      } catch (t) {
        s.MessagePlugin.error(t.message);
      } finally {
        c.value = !1;
      }
    }
    async function P(t) {
      c.value = !0;
      try {
        await C(t), t.settings.lastScan = await d(`${r}/instances/${t.id}/scan`, { method: "POST" }), s.MessagePlugin.success("扫描完成，请检查端侧分类");
      } catch (a) {
        s.MessagePlugin.error(a.message);
      } finally {
        c.value = !1;
      }
    }
    async function C(t) {
      t.settings.overrides = Object.fromEntries((t.settings.lastScan || []).map((a) => [a.path, a.side])), await d(`${r}/instances/${t.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(t.settings) });
    }
    async function $(t) {
      try {
        await C(t), await d(`${r}/instances/${t.id}/publish`, { method: "POST" }), s.MessagePlugin.success("发布成功"), await N();
      } catch (a) {
        s.MessagePlugin.error(a.message);
      }
    }
    async function O(t) {
      const a = prompt("同步码备注（例如玩家昵称）");
      if (a)
        try {
          const l = await d(`${r}/instances/${t.id}/codes`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: a }) });
          s.DialogPlugin.alert({ header: "同步码只显示一次", body: `${l.code}

请立即安全保存。` });
        } catch (l) {
          s.MessagePlugin.error(l.message);
        }
    }
    function E(t) {
      m.value = t.target.files?.[0] || null;
    }
    async function M() {
      if (!m.value) return;
      const t = new FormData();
      t.append("version", u.value), t.append("notes", v.value), t.append("file", m.value);
      try {
        await d(`${r}/launcher`, { method: "POST", body: t }), s.MessagePlugin.success("启动器正式版发布成功");
      } catch (a) {
        s.MessagePlugin.error(a.message);
      }
    }
    return e.onMounted(N), (t, a) => {
      const l = e.resolveComponent("t-alert"), p = e.resolveComponent("t-input"), g = e.resolveComponent("t-button"), V = e.resolveComponent("t-space"), w = e.resolveComponent("t-card"), A = e.resolveComponent("t-tag"), f = e.resolveComponent("t-form-item"), k = e.resolveComponent("t-select"), q = e.resolveComponent("t-form"), D = e.resolveComponent("t-checkbox"), L = e.resolveComponent("t-table"), j = e.resolveComponent("t-loading");
      return e.openBlock(), e.createElementBlock("div", { class: "page" }, [
        e.createVNode(l, {
          theme: "info",
          title: "发布流程"
        }, {
          default: e.withCtx(() => [
            e.createTextVNode("实例必须停止运行。先配置客户端版本并刷新扫描，确认端侧分类，再发布。")
          ]),
          _: 1
        }),
        e.createVNode(w, {
          title: "PCL IO 正式版",
          class: "card"
        }, {
          default: e.withCtx(() => [
            e.createVNode(V, null, {
              default: e.withCtx(() => [
                e.createVNode(p, {
                  modelValue: u.value,
                  "onUpdate:modelValue": a[0] || (a[0] = (n) => u.value = n),
                  placeholder: "版本号，如 0.2.0"
                }, null, 8, ["modelValue"]),
                e.createVNode(p, {
                  modelValue: v.value,
                  "onUpdate:modelValue": a[1] || (a[1] = (n) => v.value = n),
                  placeholder: "更新说明"
                }, null, 8, ["modelValue"]),
                e.createElementVNode("input", {
                  type: "file",
                  accept: ".exe",
                  onChange: E
                }, null, 32),
                e.createVNode(g, {
                  theme: "primary",
                  disabled: !m.value,
                  onClick: M
                }, {
                  default: e.withCtx(() => [
                    e.createTextVNode("发布启动器")
                  ]),
                  _: 1
                }, 8, ["disabled"])
              ]),
              _: 1
            })
          ]),
          _: 1
        }),
        e.createVNode(j, {
          loading: c.value,
          "show-overlay": ""
        }, {
          default: e.withCtx(() => [
            (e.openBlock(!0), e.createElementBlock(e.Fragment, null, e.renderList(i.value, (n) => (e.openBlock(), e.createBlock(w, {
              key: n.id,
              title: n.name,
              class: "card"
            }, {
              actions: e.withCtx(() => [
                e.createVNode(A, {
                  theme: n.running ? "danger" : "success"
                }, {
                  default: e.withCtx(() => [
                    e.createTextVNode(e.toDisplayString(n.running ? "运行中" : "已停止"), 1)
                  ]),
                  _: 2
                }, 1032, ["theme"])
              ]),
              default: e.withCtx(() => [
                e.createVNode(V, {
                  direction: "vertical",
                  style: { width: "100%" }
                }, {
                  default: e.withCtx(() => [
                    e.createElementVNode("div", null, "目录：" + e.toDisplayString(n.base), 1),
                    n.settings ? (e.openBlock(), e.createBlock(q, {
                      key: 0,
                      layout: "inline"
                    }, {
                      default: e.withCtx(() => [
                        e.createVNode(f, { label: "MC 版本" }, {
                          default: e.withCtx(() => [
                            e.createVNode(p, {
                              modelValue: n.settings.minecraftVersion,
                              "onUpdate:modelValue": (o) => n.settings.minecraftVersion = o,
                              placeholder: "1.21.1"
                            }, null, 8, ["modelValue", "onUpdate:modelValue"])
                          ]),
                          _: 2
                        }, 1024),
                        e.createVNode(f, { label: "加载器" }, {
                          default: e.withCtx(() => [
                            e.createVNode(k, {
                              modelValue: n.settings.loader,
                              "onUpdate:modelValue": (o) => n.settings.loader = o,
                              options: e.unref(B),
                              style: { width: "140px" }
                            }, null, 8, ["modelValue", "onUpdate:modelValue", "options"])
                          ]),
                          _: 2
                        }, 1024),
                        e.createVNode(f, { label: "加载器版本" }, {
                          default: e.withCtx(() => [
                            e.createVNode(p, {
                              modelValue: n.settings.loaderVersion,
                              "onUpdate:modelValue": (o) => n.settings.loaderVersion = o
                            }, null, 8, ["modelValue", "onUpdate:modelValue"])
                          ]),
                          _: 2
                        }, 1024),
                        e.createVNode(f, { label: "服务器地址" }, {
                          default: e.withCtx(() => [
                            e.createVNode(p, {
                              modelValue: n.settings.serverAddress,
                              "onUpdate:modelValue": (o) => n.settings.serverAddress = o,
                              placeholder: "地址:端口"
                            }, null, 8, ["modelValue", "onUpdate:modelValue"])
                          ]),
                          _: 2
                        }, 1024)
                      ]),
                      _: 2
                    }, 1024)) : e.createCommentVNode("", !0),
                    n.settings ? (e.openBlock(), e.createBlock(V, { key: 1 }, {
                      default: e.withCtx(() => [
                        e.createElementVNode("span", null, "同步目录："),
                        (e.openBlock(!0), e.createElementBlock(e.Fragment, null, e.renderList(n.settings.directories, (o) => (e.openBlock(), e.createBlock(D, {
                          key: o.path,
                          modelValue: o.enabled,
                          "onUpdate:modelValue": (h) => o.enabled = h
                        }, {
                          default: e.withCtx(() => [
                            e.createTextVNode(e.toDisplayString(o.path), 1)
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue"]))), 128))
                      ]),
                      _: 2
                    }, 1024)) : e.createCommentVNode("", !0),
                    e.createVNode(V, null, {
                      default: e.withCtx(() => [
                        e.createVNode(g, {
                          disabled: n.running,
                          onClick: (o) => P(n)
                        }, {
                          default: e.withCtx(() => [
                            e.createTextVNode("刷新扫描")
                          ]),
                          _: 1
                        }, 8, ["disabled", "onClick"]),
                        e.createVNode(g, {
                          theme: "primary",
                          disabled: n.running || !n.settings?.lastScan?.length,
                          onClick: (o) => $(n)
                        }, {
                          default: e.withCtx(() => [
                            e.createTextVNode("发布新版本")
                          ]),
                          _: 1
                        }, 8, ["disabled", "onClick"]),
                        e.createVNode(g, {
                          variant: "outline",
                          onClick: (o) => O(n)
                        }, {
                          default: e.withCtx(() => [
                            e.createTextVNode("创建同步码")
                          ]),
                          _: 1
                        }, 8, ["onClick"])
                      ]),
                      _: 2
                    }, 1024),
                    n.settings?.lastScan?.length ? (e.openBlock(), e.createBlock(L, {
                      key: 2,
                      data: n.settings.lastScan,
                      columns: U,
                      "row-key": "path"
                    }, {
                      side: e.withCtx(({ row: o }) => [
                        e.createVNode(k, {
                          modelValue: o.side,
                          "onUpdate:modelValue": (h) => o.side = h,
                          options: T,
                          onChange: (h) => C(n)
                        }, null, 8, ["modelValue", "onUpdate:modelValue", "onChange"])
                      ]),
                      _: 2
                    }, 1032, ["data"])) : e.createCommentVNode("", !0)
                  ]),
                  _: 2
                }, 1024)
              ]),
              _: 2
            }, 1032, ["title"]))), 128))
          ]),
          _: 1
        }, 8, ["loading"])
      ]);
    };
  }
}), J = (_, r) => {
  const c = _.__vccOpts || _;
  for (const [i, u] of r)
    c[i] = u;
  return c;
}, R = /* @__PURE__ */ J(K, [["__scopeId", "data-v-59e3dd38"]]), z = {
  name: "mslx-plugin-iota-sync",
  version: "1.0.1",
  routes: [{
    path: "/iota-sync",
    name: "IotaSyncBase",
    component: "HOST_LAYOUT",
    meta: { title: "客户端同步", icon: "cloud-upload", roleCode: ["admin"] },
    children: [{ path: "", name: "IotaSyncAdmin", component: R, meta: { title: "客户端同步", hidden: !0, roleCode: ["admin"] } }]
  }],
  extensions: []
};
export {
  z as pluginConfig
};
