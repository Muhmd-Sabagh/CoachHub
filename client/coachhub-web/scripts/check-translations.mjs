// Fails when a translation key is unused by the app or missing from one language (P18A dead-code check).
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const table = 'src/app/core/i18n/translations.ts';
const source = readFileSync(table, 'utf8');
const [en, ar] = source.split(/^\s{2}ar:\s*{/m);
const keysOf = (text) => new Set([...text.matchAll(/^\s+'([\w.]+)':/gm)].map((match) => match[1]));
const english = keysOf(en);
const arabic = keysOf(ar);

const files = (dir) => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? files(path) : /\.(ts|html)$/.test(name) && !path.endsWith('translations.ts') ? [path] : [];
});
const code = files('src/app').map((path) => readFileSync(path, 'utf8')).join('\n');

const problems = [
  ...[...english].filter((key) => !code.includes(`'${key}'`)).map((key) => `unused: ${key}`),
  ...[...english].filter((key) => !arabic.has(key)).map((key) => `missing in Arabic: ${key}`),
  ...[...arabic].filter((key) => !english.has(key)).map((key) => `missing in English: ${key}`),
];
if (problems.length) {
  console.error(problems.join('\n'));
  process.exit(1);
}
console.log(`Translations: ${english.size} keys, all used, both languages complete.`);
