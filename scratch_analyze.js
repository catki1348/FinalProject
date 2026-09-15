const fs = require('fs');
const path = "C:\\Users\\MBC-501-16\\.claude\\projects\\C--Users-MBC-501-16-Desktop-FinalProject\\38d5db31-2031-436d-a21f-6940efc16324\\tool-results\\mcp-df443430-fb1e-44e6-af5e-f8c7b99dfd74-get_workflow_graph-1789452041921.txt";
const data = JSON.parse(fs.readFileSync(path, 'utf8'));
const nodes = data.graph.nodes;
let maxX = -Infinity, maxY = -Infinity;
for (const n of nodes) {
  const rx = n.position.x + (n.size ? n.size.width : 0);
  const ry = n.position.y + (n.size ? n.size.height : 0);
  if (rx > maxX) maxX = rx;
  if (ry > maxY) maxY = ry;
}
console.log('nodeCount', nodes.length);
console.log('maxX', maxX, 'maxY', maxY);
const ids = ['bb21e54d-0198-48a6-b167-18069556b53a','741844a6-b0e9-41f6-a3a3-aae3681b6398','a772c32c-19f7-4621-bc75-4965327d5431','92d19a62-a686-4557-b4e4-32ed159faf9d'];
for (const id of ids) {
  const n = nodes.find(x => x.id === id);
  console.log(id, n ? JSON.stringify({type: n.type, pos: n.position, status: n.status}) : 'NOT FOUND');
}
