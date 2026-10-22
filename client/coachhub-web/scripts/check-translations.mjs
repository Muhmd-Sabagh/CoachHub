// Fails when a translation key is defined but never used by the app (P18A dead-code check, per-area tables since P19).
// Both languages having every key is enforced by the TypeScript types (core/i18n/define.ts). Enum labels
// (`enum.<name>.<Value>`) are looked up by a computed key and checked by enum-labels coverage instead.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const tablesDir = 'src/app/core/i18n/translations';
const keys = readdirSync(tablesDir).filter((name) => name.endsWith('.ts')).flatMap((name) => {
  const source = readFileSync(join(tablesDir, name), 'utf8');
  const english = source.slice(source.indexOf('en: {'), source.indexOf('ar: {'));
  return [...english.matchAll(/^\s+'([\w.-]+)':/gm)].map((match) => match[1]);
});

const files = (dir) => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  if (statSync(path).isDirectory()) return path.endsWith('translations') ? [] : files(path);
  return /\.(ts|html)$/.test(name) ? [path] : [];
});
const code = files('src/app').map((path) => readFileSync(path, 'utf8')).join('\n');

const unused = keys.filter((key) => !key.startsWith('enum.') && !code.includes(`'${key}'`));
if (unused.length) {
  console.error(unused.map((key) => `unused: ${key}`).join('\n'));
  process.exit(1);
}
console.log(`Translations: ${keys.length} keys, all used; both languages are checked by the build.`);
