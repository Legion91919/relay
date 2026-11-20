// Scripted walkthrough for the README demo. Run: node demo/record.mjs .
// Two users need two phones: the recorded page is a stage with two phone-sized frames of the real app,
// each signed in as a different demo user (Relay keeps the user per tab and messages per user).
const APP = "http://localhost:6103";
const API = "http://localhost:5103";
const CHAT = "dm.u_sipho.u_thandi";

const stage = `<!doctype html><html><head><style>
  body { margin: 0; height: 100vh; background: #e9efec; font: 600 15px system-ui, sans-serif; color: #1d2b27;
         display: flex; justify-content: center; align-items: flex-start; gap: 96px; padding-top: 18px; box-sizing: border-box; }
  figure { margin: 0; display: grid; align-content: start; gap: 8px; justify-items: center; }
  .phone { width: 380px; height: 600px; border: 10px solid #1d2b27; border-radius: 30px; overflow: hidden; background: #fff; }
  iframe { width: 100%; height: 100%; border: 0; display: block; }
  .off { height: 100%; display: grid; place-items: center; text-align: center; color: #6b7b76; font-weight: 500; padding: 24px; background: #f4f6f5; }
</style></head><body>
  <figure><figcaption>Thandi's phone</figcaption><div class="phone"><iframe id="thandi" title="Thandi" src="${APP}/?as=u_thandi&chat=${CHAT}"></iframe></div></figure>
  <figure><figcaption>Sipho's phone</figcaption><div class="phone" id="sipho-phone"><div class="off">No signal<br>Sipho's phone is offline</div></div></figure>
</body></html>`;

async function messagesHeld() {
  const session = await (await fetch(`${API}/api/identity/demo-sessions`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ userId: "u_pule" }),
  })).json();
  const res = await fetch(`${API}/api/delivery/stats`, { headers: { Authorization: `Bearer ${session.token}` } });
  if (!res.ok) throw new Error(`Delivery stats failed: ${res.status}`);
  return (await res.json()).messagesHeld;
}

export default async ({ page, step, shot }) => {
  await page.setContent(stage, { waitUntil: "load" });
  const thandi = page.frameLocator("#thandi");
  const thandiMsg = thandi.getByRole("textbox", { name: "Message" });
  await thandiMsg.waitFor();

  await step("Relay: one-to-one chat built for prepaid phones on patchy 2G/3G", async () => {});
  await shot("chat");
  await step("Thandi messages Sipho while his phone has no signal: one tick, the server has it", async () => {
    await thandiMsg.fill("Taxi rank in 10 min? I have the tickets.");
    await thandi.getByRole("button", { name: "Send" }).click();
    await thandi.getByText("Sent", { exact: true }).last().waitFor();
  });
  await shot("sent");
  await step("Sipho's phone reconnects: the message arrives and turns two grey ticks (delivered)", async () => {
    await page.evaluate(src => {
      document.getElementById("sipho-phone").innerHTML = `<iframe id="sipho" title="Sipho" src="${src}"></iframe>`;
    }, `${APP}/?as=u_sipho`);
    await thandi.getByText("Delivered", { exact: true }).last().waitFor({ timeout: 15000 });
  });
  // Proof, not a claim: ask the API how many message bodies it still stores (a bystander's session, counts only).
  const held = await messagesHeld();
  if (held !== 0) throw new Error(`Expected the server to hold 0 message bodies after delivery, got ${held}`);
  await step(`Delivered: the server now holds ${held} message bodies; only the phones keep the text`, async () => {});
  const sipho = page.frameLocator("#sipho");
  await step("Sipho opens the chat: Thandi's ticks turn blue (read)", async () => {
    await sipho.getByRole("button", { name: /Thandi Mokoena/ }).click();
    await thandi.getByText("Read", { exact: true }).last().waitFor({ timeout: 15000 });
  });
  await shot("read");
  await step("He replies, and it lands on Thandi's phone in under a second", async () => {
    await sipho.getByRole("textbox", { name: "Message" }).fill("On my way. Save me a seat!");
    await sipho.getByRole("button", { name: "Send" }).click();
    await thandi.getByText("On my way. Save me a seat!").waitFor();
  });
};

export async function mobile({ page, shot }) {
  await page.getByRole("textbox", { name: "Message" }).waitFor();
  await page.waitForTimeout(1000);
  await shot("mobile");
}
