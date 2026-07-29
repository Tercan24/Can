const fs = require("fs");
const path = require("path");
const sharp = require("sharp");

const dashboardCommit = "03e8f8e22da16ccddf5e14afa90711391357231e";
const powerToysCommit = "d72fa2ea6ea6b6f02af0a1aaeb7b85db975016d8";
const outputDirectory = path.resolve(__dirname, "..", "assets", "software");

const dashboard = (name) =>
  `https://raw.githubusercontent.com/homarr-labs/dashboard-icons/${dashboardCommit}/png/${name}.png`;
const simple = (slug, color) =>
  `https://cdn.simpleicons.org/${slug}/${color}`;

const icons = [
  ["7zip", dashboard("7zip")],
  ["chrome", dashboard("google-chrome")],
  ["firefox", dashboard("firefox")],
  ["brave", dashboard("brave")],
  ["vlc", simple("vlcmediaplayer", "FF8800")],
  ["spotify", dashboard("spotify")],
  ["notepadplusplus", simple("notepadplusplus", "90E59A")],
  [
    "powertoys",
    `https://raw.githubusercontent.com/microsoft/PowerToys/${powerToysCommit}/doc/images/icons/PowerToys%20icon/PNG/PowerToysAppList.targetsize-256.png`,
  ],
  ["steam", dashboard("steam")],
  ["epicgames", dashboard("epic-games")],
  ["discord", dashboard("discord")],
  ["ea", simple("ea", "FF4747")],
  ["ubisoft", simple("ubisoft", "4D8FF7")],
  ["gog", simple("gogdotcom", "A25BD7")],
  ["obs", simple("obsstudio", "FFFFFF")],
  ["audacity", dashboard("audacity")],
  ["sharex", simple("sharex", "4AA6E9")],
  ["qbittorrent", dashboard("qbittorrent")],
];

async function main() {
  fs.mkdirSync(outputDirectory, { recursive: true });
  const generated = [];

  for (const [key, url] of icons) {
    const response = await fetch(url, {
      headers: { "User-Agent": "tercan-software-icon-builder/1.0" },
    });
    if (!response.ok) {
      throw new Error(`${key}: ${response.status} ${response.statusText} (${url})`);
    }

    const source = Buffer.from(await response.arrayBuffer());
    const target = path.join(outputDirectory, `${key}.png`);
    await sharp(source, { density: 384 })
      .resize(96, 96, {
        fit: "contain",
        background: { r: 0, g: 0, b: 0, alpha: 0 },
      })
      .png({ compressionLevel: 9, adaptiveFiltering: true })
      .toFile(target);

    generated.push({ key, bytes: fs.statSync(target).size, source: url });
  }

  console.log(JSON.stringify(generated, null, 2));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
