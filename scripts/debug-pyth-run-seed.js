import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import process from "process";
import { ethers } from "ethers";
import dotenv from "dotenv";

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const MONAD_RPC_URL = process.env.MONAD_RPC_URL || "https://rpc.monad.xyz";
const MONAD_CHAIN_ID = 143;

// Contrat consumer Pyth déployé
const CONSUMER_ADDRESS = "0x3A04d36eeF559F5E42BD2e7896AA691ceDEcEa9e";
// Adresse joueur (wallet) à inspecter
const PLAYER_ADDRESS = (process.env.PLAYER_ADDRESS || "0xA452B9a058e976757a31B86496fBFc2b036bc858").toLowerCase();

async function main() {
  const provider = new ethers.JsonRpcProvider(MONAD_RPC_URL, MONAD_CHAIN_ID);

  console.log("RPC:", MONAD_RPC_URL);
  console.log("Consumer:", CONSUMER_ADDRESS);
  console.log("Player:", PLAYER_ADDRESS);

  const artifactPath = path.join(
    __dirname,
    "..",
    "artifacts",
    "contracts",
    "ChogZombiesEntropyConsumer.sol",
    "ChogZombiesEntropyConsumer.json"
  );

  if (!fs.existsSync(artifactPath)) {
    throw new Error(
      `Artifact not found at ${artifactPath}. Did you run \"npx hardhat compile\"?`
    );
  }

  const artifact = JSON.parse(fs.readFileSync(artifactPath, "utf8"));
  const { abi } = artifact;

  const contract = new ethers.Contract(CONSUMER_ADDRESS, abi, provider);

  console.log("Reading getLatestRunSeed...");
  const latest = await contract.getLatestRunSeed(PLAYER_ADDRESS);
  // latest: [ready, value, sequenceNumber]
  console.log("getLatestRunSeed =>", latest);

  console.log("Reading getLatestRunSeedValue...");
  const valueOnly = await contract.getLatestRunSeedValue(PLAYER_ADDRESS);
  console.log("getLatestRunSeedValue =>", valueOnly);

  const sequenceNumber = latest[2];
  console.log("sequenceNumber =>", sequenceNumber.toString());

  if (sequenceNumber > 0n) {
    console.log("Reading randomnessBySequence...");
    const info = await contract.randomnessBySequence(sequenceNumber);
    console.log("randomnessBySequence =>", info);
  } else {
    console.log("No run seed request recorded for this player (sequenceNumber == 0)");
  }
}

main().catch((err) => {
  console.error("DEBUG ERROR:", err);
  process.exitCode = 1;
});
